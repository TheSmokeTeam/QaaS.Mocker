using System.Text.Json;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.MockerObjects;
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
            "Handler '{Handler}' started. Listening on '{RequestChannel}' and publishing to '{ResponseChannel}'",
            GetType().Name, requestChannel, responseChannel);
        subscriberClient.Subscribe(requestChannel, (channel, serializedRequestMessage) =>
        {
            try
            {
                if (serializedRequestMessage.IsNullOrEmpty)
                {
                    logger.LogWarning("Ignoring empty control request on channel '{Channel}'", channel);
                    return;
                }

                var requestPayload = serializedRequestMessage.ToString();
                var request = JsonSerializer.Deserialize<TRequestMessage>(requestPayload);
                if (request == null)
                {
                    logger.LogWarning("Ignoring invalid control request on channel '{Channel}'. Payload: {Payload}",
                        channel, requestPayload);
                    return;
                }

                logger.LogInformation("Received control request on channel '{Channel}'. " +
                                      "Message: {RequestMessage}", channel, request);
                var responseMessage = HandleRequest(channel, request);
                if (responseMessage == null)
                {
                    logger.LogInformation("Empty response for the last control request");
                    return;
                }
                logger.LogInformation("Response for the last control request: {ResponseMessage}", responseMessage);
                subscriberClient.Publish(responseChannel, JsonSerializer.Serialize(responseMessage));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception handling control request on channel '{Channel}'. " +
                                           "Message: {RequestMessage}", channel, serializedRequestMessage);
            }
        });
    }
}

