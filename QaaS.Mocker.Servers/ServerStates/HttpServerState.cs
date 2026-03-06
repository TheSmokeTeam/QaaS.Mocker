using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.Actions;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Servers.ServerStates;

/// <summary>
/// TODO change docs
/// Resolves transaction stubs and processes request data.
/// </summary>
public class HttpServerState : IServerState
{
    public InputOutputState InputOutputState { get; init; } = InputOutputState.BothInputOutput;

    private const string NotFoundTransactionStub = "NotFoundTransactionStub";
    private const string InternalServerErrorTransactionStub = "InternalServerErrorTransactionStub";
    private readonly ILogger _logger;
    private readonly IImmutableList<TransactionStub> _transactionStubList;
    private readonly TransactionStub _notFoundTransactionStub;
    private readonly TransactionStub _internalErrorTransactionStub;
    private readonly IList<ActionToTransactionStub> _actionToStubList;
    private readonly IDictionary<Regex, IDictionary<HttpMethod, ActionToTransactionStub>> _httpActions;
    private readonly TransactionsCache _cache;

    private const int MatchGroupIndexOne = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServerState"/> class.
    /// </summary>
    /// <param name="logger">Logger for logging transaction stub activities.</param>
    /// <param name="transactionStubList">List of transaction stubs.</param>
    /// <param name="notFoundTransactionStubName">Name of the transaction stub for not found responses.</param>
    /// <param name="internalErrorTransactionStubName">Name of the transaction stub for internal error responses.</param>
    /// <param name="endpoints">Array of Http Endpoint configurations.</param>
    public HttpServerState(ILogger logger, IImmutableList<TransactionStub> transactionStubList,
        string? notFoundTransactionStubName, string? internalErrorTransactionStubName, HttpEndpointConfig[]? endpoints)
    {
        _logger = logger;
        _transactionStubList = transactionStubList;
        _cache = new TransactionsCache();

        notFoundTransactionStubName ??= Constants.DefaultNotFoundTransactionStubLabel;
        _notFoundTransactionStub = GetTransactionStub(notFoundTransactionStubName);

        internalErrorTransactionStubName ??= Constants.DefaultInternalErrorTransactionStubLabel;
        _internalErrorTransactionStub = GetTransactionStub(internalErrorTransactionStubName);


        _actionToStubList = new List<ActionToTransactionStub>();
        _httpActions = new Dictionary<Regex, IDictionary<HttpMethod, ActionToTransactionStub>>();

        if (endpoints == null) return;

        foreach (var endpoint in endpoints)
        {
            var pathRegex = endpoint.GeneratePathRegex();
            var methodMapping = new Dictionary<HttpMethod, ActionToTransactionStub>();
            _httpActions[pathRegex] = methodMapping;

            foreach (var endpointAction in endpoint.Actions)
            {
                var transactionStub = GetTransactionStub(endpointAction.TransactionStubName);
                var actionToTransactionStub =
                    new ActionToTransactionStub { ActionName = endpointAction.Name, Stub = transactionStub };
                methodMapping[endpointAction.Method] = actionToTransactionStub;
                if (endpointAction.Name != null) _actionToStubList.Add(actionToTransactionStub);
                else
                    _logger.LogWarning(
                        "Configured HTTP action for path regex '{PathRegex}' and method '{Method}' without an action name",
                        pathRegex, endpointAction.Method);

                _logger.LogDebug(
                    "Registered HTTP action '{ActionName}' for method '{Method}' path regex '{PathRegex}' with stub '{StubName}'",
                    endpointAction.Name ?? "<unnamed>", endpointAction.Method, pathRegex, transactionStub.Name);
            }
        }
    }

    private TransactionStub GetTransactionStub(string transactionStubName)
    {
        return _transactionStubList.FirstOrDefault(transactionStub =>
                   transactionStub.Name == transactionStubName)
               ?? throw new StubNotLoadedException
                   ($"Transaction Stub for actions: '{transactionStubName}' is not loaded!");
    }

    /// <summary>
    /// Gets the transaction stub name for the given path and HTTP method.
    /// </summary>
    /// <param name="path">Request path.</param>
    /// <param name="method">HTTP method.</param>
    /// <param name="pathParameters">The path parameter from the key</param>
    /// <returns>Tuple containing the transaction stub name</returns>
    private TransactionStub ResolveTransactionStub(string path, HttpMethod method,
        out IDictionary<string, string>? pathParameters)
    {
        foreach (var mappedEndpointPathRegex in _httpActions.Keys)
        {
            var endpointPathRegexMatchResult = mappedEndpointPathRegex.Match(path);
            if (!endpointPathRegexMatchResult.Success) continue;
            if (!_httpActions[mappedEndpointPathRegex].TryGetValue(method, out var mappedAction)) continue;

            if (endpointPathRegexMatchResult.Groups.Count > MatchGroupIndexOne)
            {
                pathParameters = new Dictionary<string, string>();
                for (var parameterGroupIndex = MatchGroupIndexOne;
                     parameterGroupIndex < endpointPathRegexMatchResult.Groups.Count;
                     parameterGroupIndex++)
                {
                    var parameterGroup = endpointPathRegexMatchResult.Groups[parameterGroupIndex];
                    pathParameters[parameterGroup.Name] = parameterGroup.Value;
                }
            }
            else pathParameters = null;

            return mappedAction.Stub;
        }

        _logger.LogWarning(
            "No HTTP action matched request '{HttpMethod} {Path}'. Falling back to stub '{StubName}'",
            method, path, _notFoundTransactionStub.Name);
        pathParameters = null;
        return _notFoundTransactionStub;
    }

    private string ResolveActionName(string path, HttpMethod method)
    {
        foreach (var mappedEndpointPathRegex in _httpActions.Keys)
        {
            var endpointPathRegexMatchResult = mappedEndpointPathRegex.Match(path);
            if (!endpointPathRegexMatchResult.Success) continue;
            if (!_httpActions[mappedEndpointPathRegex].TryGetValue(method, out var mappedAction)) continue;


            return mappedAction.ActionName!;
        }

        return NotFoundTransactionStub;
    }


    /// <summary>
    /// Processes the request data through the specified transaction stub.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="method"></param>
    /// <param name="requestData">Request data.</param>
    /// <returns>Response data.</returns>
    public Data<object> Process(string path, HttpMethod method, Data<object> requestData)
    {
        Data<object>? responseData;
        var transactionProcessedSuccessfully = true;
        var actionName = ResolveActionName(path, method);
        try
        {
            var transactionStub = ResolveTransactionStub(path, method, out var pathParameters);
            requestData.MetaData!.Http.PathParameters = pathParameters;
            _cache.StoreInput(requestData.CloneDetailed(), actionName);
            _logger.LogDebug(
                "Processing HTTP action '{ActionName}' for request '{HttpMethod} {Path}' using stub '{StubName}'",
                actionName, method, path, transactionStub.Name);
            responseData = transactionStub.Exercise(requestData);
        }
        catch (Exception exception)
        {
            responseData = null;
            _logger.LogError(exception,
                "HTTP action '{ActionName}' failed for request '{HttpMethod} {Path}'. Falling back to internal error stub '{InternalErrorTransactionStubName}'",
                actionName, method, path, _internalErrorTransactionStub.Name);
            transactionProcessedSuccessfully = false;
        }

        if (transactionProcessedSuccessfully)
        {
            _cache.StoreOutput(responseData!.CloneDetailed(), actionName);
            return responseData!;
        }

        try
        {
            responseData = _internalErrorTransactionStub.Exercise(requestData); // TODO Add Exceptions to Data<object> ?
        }
        catch (Exception exception)
        {
            throw new FatalInternalErrorException("Error within Internal Error Transaction Stub.", exception);
        }
        finally
        {
            _cache.StoreOutput(responseData?.CloneDetailed(), InternalServerErrorTransactionStub);
        }

        return responseData;
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
        _logger.LogInformation(
            "Changed HTTP action '{ActionName}' transaction stub from '{OldTransactionStub}' to '{NewTransactionStub}'",
            actionName, oldAssignedTransactionStubName, stubName);
    }

    public void TriggerAction(string actionName, int? timeoutMs)
    {
        throw new NotImplementedException("Http server does not holds any functionality needed to be triggered.");
    }

    public ICache GetCache() => _cache;
}
