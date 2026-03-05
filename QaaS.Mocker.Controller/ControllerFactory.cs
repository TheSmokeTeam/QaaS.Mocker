using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller;

public class ControllerFactory(Context context, ControllerConfig? controller)
{
    public IController? Build(IServerState serverState)
    {
        ConnectionMultiplexer redisConnection;
        var serverName = controller?.ServerName;
        if (serverName == null)
        {
            context.Logger.LogWarning("Server Name configuration wasn't given - Not initiating Controller API");
            return null;
        }
        
        try
        {   
            if (controller?.Redis != null)
                redisConnection = ConnectionMultiplexer.Connect(controller.Redis.CreateRedisConfigurationOptions());
            else
            {
                context.Logger.LogWarning("Redis API configuration wasn't given - Not initiating Controller API");
                return null;
            }
        }
        catch (RedisConnectionException exception)
        {
            context.Logger.LogError("Couldn't connect to Redis API - Not initiating Controller API. " +
                                    "Exception: {ExceptionMessage}", exception.Message);
            return null;
        }
        catch (Exception exception)
        {
            throw new ControllerInitializationException("Unexpected error while creating redis controller", exception);
        }

        var serverInstanceId = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        context.Logger.LogInformation(
            "Initialized redis controller for server '{ServerName}' with instance id '{ServerInstanceId}'",
            serverName, serverInstanceId);

        return new Controllers.Controller(
            redisConnection,
            controller.Redis!.RedisDataBase,
            serverState,
            serverName,
            serverInstanceId,
            context.Logger);
    }
}
