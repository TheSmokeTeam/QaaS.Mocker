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


    private const int ConsumePollingDelayMilliseconds = 10;
    private int _consumeState; 
    
    /// <summary>
    /// Executes a command request and wraps errors into a failed command response.
    /// </summary>
    /// <param name="channel">The request channel where the message was received.</param>
    /// <param name="request">The command request payload.</param>
    /// <returns>The command response describing execution status.</returns>
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
    
    /// <summary>
    /// Routes command payloads to the appropriate runtime action.
    /// </summary>
    /// <param name="command">The command request to execute.</param>
    /// <exception cref="ArgumentException">Thrown when a command payload is missing or invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when command type is unsupported.</exception>
    private void HandleCommand(CommandRequest command)
    {
        switch (command.Command)
        {
            case CommandType.ChangeActionStub:
            {
                var changeActionStub = ResolveChangeActionStub(command);
                serverState.ChangeActionStub(changeActionStub.ActionName!, changeActionStub.StubName!);
                break;
            }
            case CommandType.Consume:
                RunConsuming(ResolveConsume(command));
                break;
            case CommandType.TriggerAction:
            {
                var triggerAction = ResolveTriggerAction(command);
                serverState.TriggerAction(triggerAction.ActionName!, triggerAction.TimeoutMs);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(command.Command), command.Command, "Command not supported");
        }
    }

    /// <summary>
    /// Validates and returns the payload for <see cref="CommandType.ChangeActionStub"/>.
    /// </summary>
    private static ChangeActionStub ResolveChangeActionStub(CommandRequest command)
    {
        var changeActionStub = command.ChangeActionStub
                               ?? throw new ArgumentException(
                                   "ChangeActionStub payload is required for ChangeActionStub command.",
                                   nameof(command.ChangeActionStub));
        if (string.IsNullOrWhiteSpace(changeActionStub.ActionName))
            throw new ArgumentException("ChangeActionStub.ActionName is required.", nameof(changeActionStub.ActionName));
        if (string.IsNullOrWhiteSpace(changeActionStub.StubName))
            throw new ArgumentException("ChangeActionStub.StubName is required.", nameof(changeActionStub.StubName));

        return changeActionStub;
    }

    /// <summary>
    /// Validates and returns the payload for <see cref="CommandType.Consume"/>.
    /// </summary>
    private static Consume ResolveConsume(CommandRequest command)
    {
        return command.Consume
               ?? throw new ArgumentException(
                   "Consume payload is required for Consume command.",
                   nameof(command.Consume));
    }

    /// <summary>
    /// Validates and returns the payload for <see cref="CommandType.TriggerAction"/>.
    /// </summary>
    private static TriggerAction ResolveTriggerAction(CommandRequest command)
    {
        var triggerAction = command.TriggerAction
                            ?? throw new ArgumentException(
                                "TriggerAction payload is required for TriggerAction command.",
                                nameof(command.TriggerAction));
        if (string.IsNullOrWhiteSpace(triggerAction.ActionName))
            throw new ArgumentException("TriggerAction.ActionName is required.", nameof(triggerAction.ActionName));

        return triggerAction;
    }
    
    /// <summary>
    /// Starts an asynchronous consume lifecycle if one is not already running.
    /// </summary>
    /// <param name="request">Consume request configuration.</param>
    private void RunConsuming(Consume request)
    {
        if (Interlocked.CompareExchange(ref _consumeState, 1, 0) != 0)
        {
            logger.LogDebug("Consume command already running for server '{ServerName}', ignoring duplicate request",
                serverName);
            return;
        }

        logger.LogInformation("Starting consume lifecycle for server '{ServerName}' and action '{ActionName}'",
            serverName, request.ActionName);
        CreateAndDisposeConsumerLifecycle(request).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Configures cache filters and concurrently drains input/output cache streams to Redis.
    /// </summary>
    /// <param name="request">Consume request configuration.</param>
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
                consumerTasks.Add(ConsumeAsync(_serverStateCache.RetrieveFirstOrDefaultStringInput,
                    _databaseQueueNameInput, request.TimeoutMs));

            if (serverState.InputOutputState is InputOutputState.OnlyOutput or InputOutputState.BothInputOutput)
                consumerTasks.Add(ConsumeAsync(_serverStateCache.RetrieveFirstOrDefaultStringOutput,
                    _databaseQueueNameOutput, request.TimeoutMs));

            await Task.WhenAll(consumerTasks);
            logger.LogInformation("Consume lifecycle completed for server '{ServerName}' and action '{ActionName}'",
                serverName, request.ActionName);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Consume lifecycle failed for server '{ServerName}' and action '{ActionName}'",
                serverName, request.ActionName);
            throw;
        }
        finally
        {
            _serverStateCache.CachedAction = null;
            _serverStateCache.EnableStorage = false;
            Interlocked.Exchange(ref _consumeState, 0);
        }
    }
    
    /// <summary>
    /// Polls server cache for messages and pushes them to the target Redis list until timeout expires.
    /// </summary>
    /// <param name="retrieveFromCacheFunc">Function that retrieves next cached message.</param>
    /// <param name="queueName">Redis list queue name.</param>
    /// <param name="timeoutMs">Inactivity timeout in milliseconds.</param>
    private async Task ConsumeAsync(Func<string?> retrieveFromCacheFunc, string queueName, int timeoutMs)
    {
        logger.LogInformation("Started consuming from Server's cache to '{QueueName}'", queueName);
        var stopwatch = new Stopwatch();
        stopwatch.Restart();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var message = retrieveFromCacheFunc.Invoke();
            if (message == null)
            {
                await Task.Delay(ConsumePollingDelayMilliseconds);
                continue;
            }
            logger.LogDebug("Queue: {QueueName} - consuming message: '{Message}'", queueName, message);
            await databaseClient.ListRightPushAsync(queueName, message).ConfigureAwait(false);
            stopwatch.Restart();
        }
        logger.LogInformation("Stopped consuming from Server's cache to '{QueueName}'", queueName);
    }
}

