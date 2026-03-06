using Microsoft.Extensions.Logging;
using QaaS.Mocker.Controller.Handlers;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Controllers;

/// <summary>
/// The main controller class that starts the ping and command handlers.
/// </summary>
public class Controller(IConnectionMultiplexer redisConnection, int redisDataBase, IServerState serverState,
    string serverName, string serverInstanceId, ILogger logger) : IDisposable, IController
{

    /// <summary>
    /// Starts the ping and command handlers.
    /// </summary>
    public void Start()
    {
        var subscriber = redisConnection.GetSubscriber();
        var database = redisConnection.GetDatabase(redisDataBase);
        
        logger.LogInformation(
            "Starting controller for server '{ServerName}' instance '{ServerInstanceId}' using Redis configuration '{RedisConfiguration}' and database {RedisDatabase}",
            serverName, serverInstanceId, redisConnection.Configuration, redisDataBase);
        
        new PingHandler(serverState, subscriber, serverName, serverInstanceId, logger).Start();
        new CommandHandler(serverState, database, subscriber, serverName, serverInstanceId, logger).Start();
        logger.LogInformation(
            "Controller handlers started for server '{ServerName}' instance '{ServerInstanceId}'",
            serverName, serverInstanceId);

        Thread.Sleep(Timeout.Infinite);
    }

    /// <summary>
    /// Disposes the Redis client.
    /// </summary>
    public void Dispose() => redisConnection.Dispose();
}
