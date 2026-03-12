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
    // Consume is treated as a single in-flight lifecycle so the command response can reflect the
    // real outcome of one cache-drain session instead of racing multiple overlapping drains.
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
        logger.LogInformation(
            "Handling command '{Command}' for server '{ServerName}' instance '{ServerInstanceId}' (RequestId: {RequestId})",
            request.Command, serverName, serverInstanceId, request.Id ?? "<none>");
        try
        {
            HandleCommand(request);
            status = true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Command '{Command}' failed for server '{ServerName}' instance '{ServerInstanceId}' (RequestId: {RequestId})",
                request.Command, serverName, serverInstanceId, request.Id ?? "<none>");
            exceptionMessage = exception.Message;
            status = false;
        }

        var response = new CommandResponse
        {
            Id = request.Id ?? string.Empty,
            ServerInstanceId = serverInstanceId,
            Command = request.Command,
            Status = status ? Status.Succeeded : Status.Failed,
            ExceptionMessage = exceptionMessage
        };

        logger.LogInformation(
            "Completed command '{Command}' for server '{ServerName}' instance '{ServerInstanceId}' with status '{Status}' (RequestId: {RequestId})",
            request.Command, serverName, serverInstanceId, response.Status, request.Id ?? "<none>");
        return response;
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
                logger.LogInformation(
                    "Applying ChangeActionStub command for action '{ActionName}' -> stub '{StubName}'",
                    changeActionStub.ActionName, changeActionStub.StubName);
                serverState.ChangeActionStub(changeActionStub.ActionName!, changeActionStub.StubName!);
                break;
            }
            case CommandType.Consume:
                RunConsuming(ResolveConsume(command));
                break;
            case CommandType.TriggerAction:
            {
                var triggerAction = ResolveTriggerAction(command);
                logger.LogInformation(
                    "Applying TriggerAction command for action '{ActionName}' with timeout {TimeoutMs} ms",
                    triggerAction.ActionName, triggerAction.TimeoutMs);
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
    /// Starts a consume lifecycle if one is not already running.
    /// The call is completed synchronously so the returned command response reflects the actual
    /// success or failure of the Redis drain instead of only the scheduling outcome.
    /// </summary>
    /// <param name="request">Consume request configuration.</param>
    private void RunConsuming(Consume request)
    {
        if (Interlocked.CompareExchange(ref _consumeState, 1, 0) != 0)
        {
            logger.LogInformation(
                "Ignoring duplicate Consume command for server '{ServerName}' because a consume lifecycle is already active",
                serverName);
            return;
        }

        logger.LogInformation(
            "Starting consume lifecycle for server '{ServerName}' action '{ActionName}' with timeout {TimeoutMs} ms and input/output mode {InputOutputState}",
            serverName, request.ActionName ?? "<all-actions>", request.TimeoutMs, serverState.InputOutputState);
        CreateAndDisposeConsumerLifecycle(request).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Configures cache filters and concurrently drains input/output cache streams to Redis.
    /// Cache capture is enabled only for the requested action during this lifecycle and is always
    /// cleaned up in the <c>finally</c> block so retries start from a known state.
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
            logger.LogDebug(
                "Enabled cache capture for server '{ServerName}' action '{ActionName}'. Input filter configured: {HasInputFilter}. Output filter configured: {HasOutputFilter}",
                serverName,
                request.ActionName ?? "<all-actions>",
                request.InputDataFilter != null,
                request.OutputDataFilter != null);
            var consumerTasks = new List<Task>();

            if (serverState.InputOutputState is InputOutputState.OnlyInput or InputOutputState.BothInputOutput)
                consumerTasks.Add(ConsumeAsync(_serverStateCache.RetrieveFirstOrDefaultStringInput,
                    _databaseQueueNameInput, request.TimeoutMs));

            if (serverState.InputOutputState is InputOutputState.OnlyOutput or InputOutputState.BothInputOutput)
                consumerTasks.Add(ConsumeAsync(_serverStateCache.RetrieveFirstOrDefaultStringOutput,
                    _databaseQueueNameOutput, request.TimeoutMs));

            await Task.WhenAll(consumerTasks);
            logger.LogInformation(
                "Consume lifecycle completed for server '{ServerName}' action '{ActionName}'",
                serverName, request.ActionName ?? "<all-actions>");
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Consume lifecycle failed for server '{ServerName}' action '{ActionName}'",
                serverName, request.ActionName ?? "<all-actions>");
            throw;
        }
        finally
        {
            _serverStateCache.CachedAction = null;
            _serverStateCache.EnableStorage = false;
            Interlocked.Exchange(ref _consumeState, 0);
            logger.LogDebug("Disabled cache capture for server '{ServerName}' after consume lifecycle completion",
                serverName);
        }
    }
    
    /// <summary>
    /// Polls server cache for messages and pushes them to the target Redis list until timeout expires.
    /// The inactivity timer is reset after every successful push, which lets a single consume
    /// command drain bursty traffic without holding the channel open indefinitely.
    /// </summary>
    /// <param name="retrieveFromCacheFunc">Function that retrieves next cached message.</param>
    /// <param name="queueName">Redis list queue name.</param>
    /// <param name="timeoutMs">Inactivity timeout in milliseconds.</param>
    private async Task ConsumeAsync(Func<string?> retrieveFromCacheFunc, string queueName, int timeoutMs)
    {
        logger.LogInformation(
            "Started draining cached messages for server '{ServerName}' into queue '{QueueName}' with inactivity timeout {TimeoutMs} ms",
            serverName, queueName, timeoutMs);
        var inactivityStopwatch = Stopwatch.StartNew();
        var totalStopwatch = Stopwatch.StartNew();
        var messagesConsumed = 0;
        while (inactivityStopwatch.ElapsedMilliseconds < timeoutMs)
        {
            var message = retrieveFromCacheFunc.Invoke();
            if (message == null)
            {
                await Task.Delay(ConsumePollingDelayMilliseconds);
                continue;
            }
            logger.LogDebug(
                "Pushing cached message {MessageNumber} from server '{ServerName}' to queue '{QueueName}' ({MessageLength} chars)",
                messagesConsumed + 1, serverName, queueName, message.Length);
            await databaseClient.ListRightPushAsync(queueName, message).ConfigureAwait(false);
            messagesConsumed++;
            inactivityStopwatch.Restart();
        }
        logger.LogInformation(
            "Stopped draining cached messages for server '{ServerName}' into queue '{QueueName}' after pushing {MessagesConsumed} message(s) over {ElapsedMs} ms",
            serverName, queueName, messagesConsumed, totalStopwatch.ElapsedMilliseconds);
    }
}

