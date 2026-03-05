using System.Text.Json;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.MockerObjects;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

public abstract class BaseHandler<TRequestMessage, TResponseMessage>(
    ISubscriber subscriberClient,
    string serverName,
    string serverInstanceId,
    ILogger logger)
{
    protected abstract string ContentType { get; }
    protected virtual string RequestChannel() =>
        CommunicationMethods.CreateChannelRunnerToMocker(ContentType, serverName, serverInstanceId);
    protected virtual string ResponseChannel() =>
        CommunicationMethods.CreateChannelMockerToRunner(ContentType, serverName, serverInstanceId);
    
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
                if (serializedRequestMessage == string.Empty)
                {
                    logger.LogWarning("Ignoring empty control request on channel '{Channel}'", channel);
                    return;
                }
                var request = JsonSerializer.Deserialize<TRequestMessage>((string)serializedRequestMessage!);
                logger.LogInformation("Received control request on channel '{Channel}'. " +
                                      "Message: {RequestMessage}", channel, request);
                var responseMessage = HandleRequest(channel, request!);
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

