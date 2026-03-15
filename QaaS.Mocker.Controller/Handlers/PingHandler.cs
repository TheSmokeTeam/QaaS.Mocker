using Microsoft.Extensions.Logging;
using Qaas.Mocker.CommunicationObjects;
using Qaas.Mocker.CommunicationObjects.ConfigurationObjects.Ping;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

public class PingHandler(
    IServerState serverState,
    ISubscriber subscriberClient,
    string serverName,
    string serverInstanceId,
    ILogger logger)
    : BaseHandler<PingRequest, PingResponse>(subscriberClient, serverName, serverInstanceId, logger)
{
    protected override string ContentType => "ping";
    protected override string RequestChannel() => 
        CommunicationMethods.CreateChannelRunnerToMocker(ContentType, serverName);

    protected override string ResponseChannel() =>
        CommunicationMethods.CreateChannelMockerToRunner(ContentType, serverName);
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
