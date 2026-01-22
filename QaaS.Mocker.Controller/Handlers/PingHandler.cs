using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.MockerObjects;
using QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Ping;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

public class PingHandler(IServerState serverState, ISubscriber subscriberClient, string serverName, ILogger logger) 
    : BaseHandler<PingRequest, PingResponse>(subscriberClient, serverName, logger)
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
            ServerInstanceId = Environment.MachineName,
            ServerInputOutputState = serverState.InputOutputState
        };
    }
}