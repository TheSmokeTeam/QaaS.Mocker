using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.Actions;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.Extensions;
using QaaS.Mocker.Stubs.Stubs;


namespace QaaS.Mocker.Servers.ServerStates;

public class SocketServerState : IServerState
{
    public InputOutputState InputOutputState { get; init; }

    private readonly TransactionsCache _cache = new();

    private readonly IDictionary<int, ActionState<InputOutputState>> _socketActions;
    private readonly ILogger _logger;
    private readonly IImmutableList<DataSource> _dataSourceList;
    private readonly IImmutableList<TransactionStub> _transactionStubList;
    private readonly SocketEndpointConfig[] _endpoints;

    public SocketServerState(ILogger logger, IImmutableList<DataSource> dataSourceList,
        IImmutableList<TransactionStub> transactionStubList, SocketEndpointConfig[] endpoints)
    {
        InputOutputState = endpoints.Any(endpointConfig => endpointConfig.Action!.Method == SocketMethod.Collect)
            ? endpoints.Any(method => method.Action!.Method == SocketMethod.Broadcast)
                ? InputOutputState.BothInputOutput
                : InputOutputState.OnlyInput
            : InputOutputState.OnlyOutput;
        _logger = logger;
        _dataSourceList = dataSourceList;
        _transactionStubList = transactionStubList;
        _endpoints = endpoints;
        _socketActions = endpoints
            .ToDictionary(config => config.Port!.Value, config =>
                {
                    ActionState<InputOutputState> actionState = new()
                    {
                        ActionName = config.Action!.Name,
                        DefaultEnabled = config.Action!.Method == SocketMethod.Collect,
                        State = config.Action!.Method switch
                        {
                            SocketMethod.Collect => InputOutputState.OnlyInput,
                            SocketMethod.Broadcast => InputOutputState.OnlyOutput,
                            _ => throw new ArgumentOutOfRangeException(
                                $"Encountered unknown {nameof(SocketMethod)} - {config.Action!.Method} while mapping endpoints to {nameof(InputOutputState)}")
                        }
                    };
                    actionState.Enabled = actionState.DefaultEnabled;
                    if (!string.IsNullOrEmpty(config.Action.TransactionStubName))
                        actionState.Stub = GetTransactionStub(config.Action.TransactionStubName);
                    logger.LogDebug(
                        "Registered socket action '{ActionName}' on port {Port} with method '{SocketMethod}', default enabled: {DefaultEnabled}, stub: '{StubName}', data source: '{DataSourceName}'",
                        actionState.ActionName,
                        config.Port,
                        config.Action.Method,
                        actionState.DefaultEnabled,
                        actionState.Stub?.Name ?? "<none>",
                        config.Action.DataSourceName ?? "<none>");
                    return actionState;
                }
            );
    }

    /// <summary>
    /// Processes data by <see cref="TransactionStub"/>> or <see cref="DataSource"/> and stores in
    /// cache by server's settings - <see cref="InputOutputState"/>
    /// </summary>
    /// <param name="port">Endpoint's port to resolve stub or datasource by</param>
    /// <param name="dataToProcess">The actual data to process</param>
    /// <returns>Processed data</returns>
    public IEnumerable<Data<object>> Process(int port, IEnumerable<Data<object>> dataToProcess)
    {
        var stub = ResolveTransactionStub(port);
        // If input-output state is not defined in given port - use property's value of current instance.
        var actionExists = _socketActions.TryGetValue(port, out var state);
        var inputOutputState = actionExists ? state!.State : InputOutputState;
        var actionName = state?.ActionName ?? "NotFoundTransactionStub";
        foreach (var data in dataToProcess)
        {
            if (inputOutputState is InputOutputState.OnlyInput or InputOutputState.BothInputOutput)
                _cache.StoreInput(data.CloneDetailed(), actionName);
            Data<object> processedData;
            try
            {
                _logger.LogDebug(
                    "Processing socket action '{ActionName}' on port {Port} with input/output mode '{InputOutputState}' using stub '{StubName}'",
                    actionName, port, inputOutputState, stub?.Name ?? "<pass-through>");
                processedData = stub != null ? stub.Exercise(data) : data;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Socket action '{ActionName}' on port {Port} failed during stub processing. Returning original payload.",
                    actionName, port);
                processedData = data;
            }

            if (inputOutputState is InputOutputState.OnlyOutput or InputOutputState.BothInputOutput)
                _cache.StoreOutput(processedData.CloneDetailed(), actionName);
            yield return processedData;
        }
    }

    /// <summary>
    /// Overload Process call by processing (adding to cache) generated data from configured <see cref="DataSource"/>
    /// </summary>
    /// <see cref="System.Diagnostics.Process"/>
    public IEnumerable<Data<object>> Process(int port)
    {
        _logger.LogInformation(
            "Generating socket output for port {Port} from configured data source '{DataSourceName}'",
            port,
            _endpoints.FirstOrDefault(config => config.Port == port)?.Action?.DataSourceName ?? "<unknown>");
        return Process(port, ResolveDataSource(port).Retrieve());
    }

    /// <summary>
    /// Resolves stub by port
    /// </summary>
    private TransactionStub? ResolveTransactionStub(int port)
    {
        return !_socketActions.TryGetValue(port, out var actionState) ? null : actionState.Stub;
    }

    /// <summary>
    /// Resolves data source by port
    /// </summary>
    private DataSource ResolveDataSource(int port) =>
        _dataSourceList.FirstOrDefault(source =>
            source.Name.Equals(_endpoints.FirstOrDefault(config => config.Port == port)
                ?.Action?.DataSourceName, StringComparison.OrdinalIgnoreCase))
        ?? throw new Exception(
            $"DataSource for Socket server's port: '{port}' is not loaded!"); // TODO - Add matching exception

    /// <summary>
    /// Gets stub by name
    /// </summary>
    private TransactionStub GetTransactionStub(string transactionStubName)
    {
        return _transactionStubList.FirstOrDefault(transactionStub =>
                   transactionStub.Name.Equals(transactionStubName, StringComparison.OrdinalIgnoreCase))
               ?? throw new StubNotLoadedException
                   ($"Transaction Stub for actions: '{transactionStubName}' is not loaded!");
    }

    /// <summary>
    /// Implementation of ChangeActionStub command on SocketServer.
    /// </summary>
    public void ChangeActionStub(string actionName, string stubName)
    {
        var actionState = _socketActions.Values.FirstOrDefault(state =>
            string.Equals(state.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
        if (actionState == null)
            throw new ActionDoesNotExistException($"Cannot change action '{actionName}' that doesn't exist");

        var newAssignedTransactionStub = GetTransactionStub(stubName);
        var oldAssignedTransactionStubName = actionState.Stub?.Name;
        actionState.Stub = newAssignedTransactionStub;
        _logger.LogInformation(
            "Changed socket action '{ActionName}' transaction stub from '{OldTransactionStub}' to '{NewTransactionStub}'",
            actionName, oldAssignedTransactionStubName ?? "<none>", stubName);
    }

    /// <summary>
    /// Implementation of TriggerAction command on SocketServer, the configured action will be triggered to
    /// perform for the given interval in milliseconds.
    /// </summary>
    public void TriggerAction(string actionName, int? timeoutMs)
    {
        var actionState = _socketActions.Values.FirstOrDefault(state =>
            string.Equals(state.ActionName, actionName, StringComparison.OrdinalIgnoreCase));
        if (actionState == null)
            throw new ActionDoesNotExistException($"Cannot trigger action '{actionName}' that doesn't exist");

        _logger.LogInformation(
            "Triggering socket action '{ActionName}' for {TimeoutMs} ms (default enabled: {DefaultEnabled})",
            actionName, timeoutMs.GetValueOrDefault(), actionState.DefaultEnabled);
        _ = actionState.SetEnabledForTimeoutMs(timeoutMs.GetValueOrDefault());
    }

    public bool IsEndpointPortActionEnabled(int port)
    {
        return _socketActions[port].Enabled;
    }

    public ICache GetCache()
    {
        return _cache;
    }
}
