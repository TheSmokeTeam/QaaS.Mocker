using System.Diagnostics;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ConfigurationObjects;
using Qaas.Mocker.CommunicationObjects;
using Qaas.Mocker.CommunicationObjects.ConfigurationObjects.Command;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Handlers;

/// <summary>
/// Handler class that handles command messages.
/// </summary>
public class CommandHandler(
    IServerState serverState,
    IDatabase databaseClient,
    ISubscriber subscriberClient,
    string serverName,
    string serverInstanceId,
    ILogger logger) :
    BaseHandler<CommandRequest, CommandResponse>(subscriberClient, serverName, serverInstanceId, logger)
{
    protected override string ContentType => "command";
    
    private readonly ICache _serverStateCache = serverState.GetCache();

    private readonly string _databaseQueueNameInput =
        CommunicationMethods.CreateConsumerEndpointInput(serverName);
    
    private readonly string _databaseQueueNameOutput = 
        CommunicationMethods.CreateConsumerEndpointOutput(serverName);


    private int _consumeState; 
    
    protected override CommandResponse? HandleRequest(RedisChannel channel, CommandRequest request)
    {
        bool status;
        string? exceptionMessage = null;
        try
        {
            HandleCommand(request);
            status = true;
        }
        catch (Exception exception)
        {
            logger.LogError("Couldn't handle command '{Command}': {message}", 
                request.Command, exception.Message);
            exceptionMessage = exception.Message;
            status = false;
        }
        
        return new CommandResponse
        {
            Id = request.Id,
            ServerInstanceId = serverInstanceId,
            Command = request.Command,
            Status = status ? Status.Succeeded : Status.Failed,
            ExceptionMessage = exceptionMessage
        };
    }

    private void HandleCommand(CommandRequest command)
    {
        switch (command.Command)
        {
            case CommandType.ChangeActionStub:
                serverState.ChangeActionStub(command.ChangeActionStub!.ActionName, command.ChangeActionStub.StubName);
                break;
            case CommandType.Consume:
                StartConsuming(command.Consume!);
                break;
            case CommandType.TriggerAction:
                serverState.TriggerAction(command.TriggerAction!.ActionName!, command.TriggerAction.TimeoutMs);
                break;
            default:
                throw new ArgumentException("Command not supported", command.Command.ToString());
        }
    }
    
    private void StartConsuming(Consume request)
    {
        if (Interlocked.CompareExchange(ref _consumeState, 1, 0) != 0)
        {
            logger.LogDebug("Consume command already running for server '{ServerName}', ignoring duplicate request",
                serverName);
            return;
        }

        logger.LogInformation("Starting consume lifecycle for server '{ServerName}' and action '{ActionName}'",
            serverName, request.ActionName);
        Task.Run(() => CreateAndDisposeConsumerLifecycle(request));
    }

    private async Task CreateAndDisposeConsumerLifecycle(Consume request)
    {
        try
        {
            _serverStateCache.InputDataFilter = request.InputDataFilter;
            _serverStateCache.OutputDataFilter = request.OutputDataFilter;
            _serverStateCache.EnableStorage = true;
            _serverStateCache.CachedAction = request.ActionName;
            var consumerTasks = new List<Task>();

            if (serverState.InputOutputState is InputOutputState.OnlyInput or InputOutputState.BothInputOutput)
                consumerTasks.Add(Task.Run(() =>
                    Consume(_serverStateCache.RetrieveFirstOrDefaultStringInput, _databaseQueueNameInput,
                        request.TimeoutMs)));

            if (serverState.InputOutputState is InputOutputState.OnlyOutput or InputOutputState.BothInputOutput)
                consumerTasks.Add(Task.Run(() =>
                    Consume(_serverStateCache.RetrieveFirstOrDefaultStringOutput, _databaseQueueNameOutput,
                        request.TimeoutMs)));

            await Task.WhenAll(consumerTasks);
            logger.LogInformation("Consume lifecycle completed for server '{ServerName}' and action '{ActionName}'",
                serverName, request.ActionName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Consume lifecycle failed for server '{ServerName}' and action '{ActionName}'",
                serverName, request.ActionName);
        }
        finally
        {
            _serverStateCache.CachedAction = null;
            _serverStateCache.EnableStorage = false;
            Interlocked.Exchange(ref _consumeState, 0);
        }
    }
    
    private void Consume(Func<string?> retrieveFromCacheFunc, string queueName, int timeoutMs)
    {
        logger.LogInformation("Started consuming from Server's cache to '{QueueName}'", queueName);
        var stopwatch = new Stopwatch();
        stopwatch.Restart();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var message = retrieveFromCacheFunc.Invoke();
            if (message == null) continue;
            logger.LogDebug("Queue: {QueueName} - consuming message: '{Message}'", queueName, message);
            databaseClient.ListRightPush(queueName, message);
            stopwatch.Restart();
        }
        logger.LogInformation("Stopped consuming from Server's cache to '{QueueName}'", queueName);
    }
}

