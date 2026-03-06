using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Qaas.Mocker.CommunicationObjects;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

/// <summary>
/// Base implementation for Redis pub/sub control handlers.
/// It subscribes to a request channel, deserializes requests, delegates handling,
/// and publishes a serialized response when available.
/// </summary>
public abstract class BaseHandler<TRequestMessage, TResponseMessage>(
    ISubscriber subscriberClient,
    string serverName,
    string serverInstanceId,
    ILogger logger)
{
    private static readonly JsonSerializerOptions DeserializationOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    /// <summary>
    /// Gets the semantic payload type routed by this handler (for channel naming).
    /// </summary>
    protected abstract string ContentType { get; }

    /// <summary>
    /// Builds the Redis request channel for incoming control messages.
    /// </summary>
    protected virtual string RequestChannel() =>
        CommunicationMethods.CreateChannelRunnerToMocker(ContentType, serverName, serverInstanceId);

    /// <summary>
    /// Builds the Redis response channel for outgoing control responses.
    /// </summary>
    protected virtual string ResponseChannel() =>
        CommunicationMethods.CreateChannelMockerToRunner(ContentType, serverName, serverInstanceId);
    
    /// <summary>
    /// Handles a single deserialized request message and returns a response to publish.
    /// </summary>
    /// <param name="channel">The Redis channel where the request was received.</param>
    /// <param name="requestMessage">The deserialized request payload.</param>
    /// <returns>A response payload to publish, or <c>null</c> to suppress response publication.</returns>
    protected abstract TResponseMessage? HandleRequest(RedisChannel channel, TRequestMessage requestMessage);
    
    /// <summary>
    /// Starts the handler.
    /// </summary>
    public void Start()
    {
        var requestChannel = RequestChannel();
        var responseChannel = ResponseChannel();
        logger.LogInformation(
            "Started control handler '{Handler}' for content type '{ContentType}'. Request channel '{RequestChannel}', response channel '{ResponseChannel}'",
            GetType().Name, ContentType, requestChannel, responseChannel);
        subscriberClient.Subscribe(requestChannel, (channel, serializedRequestMessage) =>
        {
            try
            {
                if (serializedRequestMessage.IsNullOrEmpty)
                {
                    logger.LogWarning(
                        "Ignoring empty '{ContentType}' control request on channel '{Channel}'",
                        ContentType, channel);
                    return;
                }

                var requestPayload = serializedRequestMessage.ToString();
                var request = JsonSerializer.Deserialize<TRequestMessage>(requestPayload, DeserializationOptions);
                if (request == null)
                {
                    logger.LogWarning(
                        "Ignoring invalid '{ContentType}' control request on channel '{Channel}' ({PayloadLength} chars)",
                        ContentType, channel, requestPayload.Length);
                    logger.LogDebug(
                        "Invalid '{ContentType}' control payload on channel '{Channel}': {Payload}",
                        ContentType, channel, requestPayload);
                    return;
                }

                logger.LogInformation(
                    "Received '{ContentType}' control request on channel '{Channel}' ({PayloadLength} chars)",
                    ContentType, channel, requestPayload.Length);
                logger.LogDebug(
                    "Deserialized '{ContentType}' request from channel '{Channel}': {RequestMessage}",
                    ContentType, channel, request);
                var responseMessage = HandleRequest(channel, request);
                if (responseMessage == null)
                {
                    logger.LogInformation(
                        "Handled '{ContentType}' control request on channel '{Channel}' without publishing a response",
                        ContentType, channel);
                    return;
                }
                var responsePayload = JsonSerializer.Serialize(responseMessage);
                logger.LogInformation(
                    "Publishing '{ContentType}' control response to channel '{ResponseChannel}' ({PayloadLength} chars)",
                    ContentType, responseChannel, responsePayload.Length);
                logger.LogDebug(
                    "Serialized '{ContentType}' response for channel '{ResponseChannel}': {ResponseMessage}",
                    ContentType, responseChannel, responseMessage);
                subscriberClient.Publish(responseChannel, responsePayload);
            }
            catch (Exception exception)
            {
                logger.LogError(exception,
                    "Failed to handle '{ContentType}' control request on channel '{Channel}'. Raw payload: {RequestMessage}",
                    ContentType, channel, serializedRequestMessage);
            }
        });
    }
}
