using Microsoft.Extensions.Logging;
using QaaS.Mocker.Controller.Handlers;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Controllers;

/// <summary>
/// The main controller class that starts the ping and command handlers.
/// </summary>
public class Controller(IConnectionMultiplexer redisConnection, int redisDataBase, IServerState serverState, 
    string serverName, ILogger logger) : IDisposable, IController
{

    /// <summary>
    /// Starts the ping and command handlers.
    /// </summary>
    public void Start()
    {
        var subscriber = redisConnection.GetSubscriber();
        var database = redisConnection.GetDatabase(redisDataBase);
        
        logger.LogInformation("Controller started and connected {RedisConnectionConfiguration} " +
                              "at database {RedisDatabase}", redisConnection.Configuration, redisDataBase);
        
        new PingHandler(serverState, subscriber, serverName, logger).Start();
        new CommandHandler(serverState, database, subscriber, serverName, logger).Start();

        while (true) {}
    }

    /// <summary>
    /// Disposes the Redis client.
    /// </summary>
    public void Dispose() => redisConnection.Dispose();
}