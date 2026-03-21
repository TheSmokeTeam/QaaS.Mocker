using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller;

/// <summary>
/// Builds the optional Redis-backed controller for a configured mocker server state.
/// </summary>
public class ControllerFactory(Context context, ControllerConfig? controller)
{
    /// <summary>
    /// Creates the controller when both a server name and valid Redis settings are available.
    /// </summary>
    public IController? Build(IServerState serverState)
    {
        ConnectionMultiplexer redisConnection;
        var serverName = controller?.ServerName;
        if (serverName == null)
        {
            context.Logger.LogInformation(
                "Controller startup skipped because 'Controller.ServerName' is not configured.");
            return null;
        }

        try
        {
            if (controller?.Redis != null)
            {
                context.Logger.LogInformation(
                    "Connecting controller to Redis for server '{ServerName}' using host '{RedisHost}' and database {RedisDatabase}",
                    serverName,
                    controller.Redis.Host,
                    controller.Redis.RedisDataBase);
                redisConnection = ConnectionMultiplexer.Connect(controller.Redis.CreateRedisConfigurationOptions());
            }
            else
            {
                context.Logger.LogWarning(
                    "Controller startup skipped for server '{ServerName}' because Redis configuration is missing.",
                    serverName);
                return null;
            }
        }
        catch (RedisConnectionException exception)
        {
            context.Logger.LogError(exception,
                "Controller startup failed for server '{ServerName}' because Redis connection could not be established.",
                serverName);
            return null;
        }
        catch (Exception exception)
        {
            throw new ControllerInitializationException("Unexpected error while creating redis controller", exception);
        }

        var serverInstanceId = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
        context.Logger.LogInformation(
            "Initialized Redis controller for server '{ServerName}' with instance id '{ServerInstanceId}' on database {RedisDatabase}",
            serverName, serverInstanceId, controller.Redis!.RedisDataBase);

        return new Controllers.Controller(
            redisConnection,
            controller.Redis!.RedisDataBase,
            serverState,
            serverName,
            serverInstanceId,
            context.Logger);
    }
}
