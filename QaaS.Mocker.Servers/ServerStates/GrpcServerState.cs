using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.Actions;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.Extensions;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.ServerStates;

public class GrpcServerState : IServerState
{
    public InputOutputState InputOutputState { get; init; } = InputOutputState.BothInputOutput;

    private const string NotFoundTransactionStub = "NotFoundTransactionStub";
    private const string InternalServerErrorTransactionStub = "InternalServerErrorTransactionStub";

    private readonly ILogger _logger;
    private readonly IImmutableList<TransactionStub> _transactionStubList;
    private readonly TransactionStub _notFoundTransactionStub;
    private readonly TransactionStub _internalErrorTransactionStub;
    private readonly IList<ActionToTransactionStub> _actionToStubList;
    private readonly IDictionary<string, ActionToTransactionStub> _rpcToAction;
    private readonly TransactionsCache _cache;

    public GrpcServerState(
        ILogger logger,
        IImmutableList<TransactionStub> transactionStubList,
        string? notFoundTransactionStubName,
        string? internalErrorTransactionStubName,
        GrpcServiceConfig[] services)
    {
        _logger = logger;
        _transactionStubList = transactionStubList;
        _cache = new TransactionsCache();
        _actionToStubList = new List<ActionToTransactionStub>();
        _rpcToAction = new Dictionary<string, ActionToTransactionStub>(StringComparer.OrdinalIgnoreCase);

        notFoundTransactionStubName ??= Constants.DefaultNotFoundTransactionStubLabel;
        _notFoundTransactionStub = GetTransactionStub(notFoundTransactionStubName);

        internalErrorTransactionStubName ??= Constants.DefaultInternalErrorTransactionStubLabel;
        _internalErrorTransactionStub = GetTransactionStub(internalErrorTransactionStubName);

        foreach (var service in services)
        {
            foreach (var action in service.Actions)
            {
                var transactionStub = GetTransactionStub(action.TransactionStubName);
                var actionToStub = new ActionToTransactionStub
                {
                    ActionName = action.Name ?? $"{service.ServiceName}.{action.RpcName}",
                    Stub = transactionStub
                };

                _rpcToAction[BuildRpcKey(service.ServiceName, action.RpcName)] = actionToStub;
                _actionToStubList.Add(actionToStub);
            }
        }
    }

    public Data<object> Process(string serviceName, string rpcName, Data<object> requestData)
    {
        Data<object>? responseData;
        var processedSuccessfully = true;
        var actionName = ResolveActionName(serviceName, rpcName);

        try
        {
            var transactionStub = ResolveTransactionStub(serviceName, rpcName);
            _cache.StoreInput(requestData.CloneDetailed(), actionName);
            _logger.LogDebug("Handling grpc request Service '{ServiceName}' Rpc '{RpcName}'", serviceName, rpcName);
            responseData = transactionStub.Exercise(requestData);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Encountered exception handling grpc request Service '{ServiceName}' Rpc '{RpcName}'. " +
                "Processing through internal error transaction stub: {InternalErrorTransactionStubName}.",
                serviceName, rpcName, _internalErrorTransactionStub.Name);
            processedSuccessfully = false;
            responseData = null;
        }

        if (processedSuccessfully)
        {
            _cache.StoreOutput(responseData!.CloneDetailed(), actionName);
            return responseData;
        }

        try
        {
            responseData = _internalErrorTransactionStub.Exercise(requestData);
        }
        catch (Exception exception)
        {
            throw new FatalInternalErrorException("Error within Internal Error Transaction Stub.", exception);
        }
        finally
        {
            _cache.StoreOutput(responseData?.CloneDetailed(), InternalServerErrorTransactionStub);
        }

        return responseData!;
    }

    public void ChangeActionStub(string actionName, string stubName)
    {
        var actionToTransactionStub = _actionToStubList
            .FirstOrDefault(pair => string.Equals(pair.ActionName, actionName, StringComparison.OrdinalIgnoreCase));

        if (actionToTransactionStub == null)
            throw new ActionDoesNotExistException($"Cannot change action '{actionName}' that doesn't exist");

        var newAssignedTransactionStub = GetTransactionStub(stubName);
        var oldAssignedTransactionStubName = actionToTransactionStub.Stub.Name;
        actionToTransactionStub.Stub = newAssignedTransactionStub;
        _logger.LogInformation("Successfully changed action '{ActionName}'s transaction stub from " +
                               "'{OldTransactionStub}' to '{NewTransactionStub}'",
            actionName, oldAssignedTransactionStubName, stubName);
    }

    public void TriggerAction(string actionName, int? timeoutMs)
    {
        throw new NotImplementedException("Grpc server does not hold actions that require trigger semantics.");
    }

    public ICache GetCache() => _cache;

    private TransactionStub ResolveTransactionStub(string serviceName, string rpcName)
    {
        if (_rpcToAction.TryGetValue(BuildRpcKey(serviceName, rpcName), out var actionToStub))
            return actionToStub.Stub;

        _logger.LogWarning("Encountered unknown grpc request Service '{ServiceName}' Rpc '{RpcName}'", serviceName,
            rpcName);
        return _notFoundTransactionStub;
    }

    private string ResolveActionName(string serviceName, string rpcName)
    {
        return _rpcToAction.TryGetValue(BuildRpcKey(serviceName, rpcName), out var actionToStub)
            ? actionToStub.ActionName ?? NotFoundTransactionStub
            : NotFoundTransactionStub;
    }

    private TransactionStub GetTransactionStub(string transactionStubName)
    {
        return _transactionStubList.FirstOrDefault(transactionStub =>
                   transactionStub.Name.Equals(transactionStubName, StringComparison.OrdinalIgnoreCase))
               ?? throw new StubNotLoadedException(
                   $"Transaction Stub for action '{transactionStubName}' is not loaded!");
    }

    private static string BuildRpcKey(string serviceName, string rpcName) => $"{serviceName}/{rpcName}";
}
