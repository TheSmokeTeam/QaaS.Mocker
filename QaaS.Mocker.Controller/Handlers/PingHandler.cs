using Microsoft.Extensions.Logging;
using Qaas.Mocker.CommunicationObjects;
using Qaas.Mocker.CommunicationObjects.ConfigurationObjects.Ping;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

/// <summary>
/// Responds to controller ping requests with runtime identity and input/output capabilities.
/// </summary>
public class PingHandler(
    IServerState serverState,
    ISubscriber subscriberClient,
    string serverName,
    string serverInstanceId,
    ILogger logger)
    : BaseHandler<PingRequest, PingResponse>(subscriberClient, serverName, serverInstanceId, logger)
{
    /// <summary>
    /// Gets the logical handler content type used for channel naming.
    /// </summary>
    protected override string ContentType => "ping";

    /// <summary>
    /// Uses the shared server-level ping channel instead of instance-specific routing.
    /// </summary>
    protected override string RequestChannel() =>
        CommunicationMethods.CreateChannelRunnerToMocker(ContentType, serverName);

    /// <summary>
    /// Uses the shared server-level response channel for ping acknowledgements.
    /// </summary>
    protected override string ResponseChannel() =>
        CommunicationMethods.CreateChannelMockerToRunner(ContentType, serverName);

    /// <summary>
    /// Creates the ping response payload from the current runtime state.
    /// </summary>
    protected override PingResponse? HandleRequest(RedisChannel channel, PingRequest request)
    {
        return new PingResponse
        {
            Id = request.Id,
            ServerName = serverName,
            ServerInstanceId = serverInstanceId,
            ServerInputOutputState = serverState.InputOutputState
        };
    }
}
