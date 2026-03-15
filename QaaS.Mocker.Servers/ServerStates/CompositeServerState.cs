using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.Exceptions;

namespace QaaS.Mocker.Servers.ServerStates;

/// <summary>
/// Routes controller commands and cache access across multiple server transports.
/// </summary>
public sealed class CompositeServerState(IEnumerable<IServerState> serverStates) : IServerState
{
    private readonly IServerState[] _serverStates = serverStates.ToArray();
    private readonly CompositeCache _cache = new(serverStates.Select(serverState => serverState.GetCache()));

    public InputOutputState InputOutputState { get; init; } = ResolveInputOutputState(serverStates);

    public bool HasAction(string actionName)
    {
        return _serverStates.Any(serverState => serverState.HasAction(actionName));
    }

    public void ChangeActionStub(string actionName, string stubName)
    {
        ResolveSingleActionState(actionName, nameof(ChangeActionStub)).ChangeActionStub(actionName, stubName);
    }

    public void TriggerAction(string actionName, int? timeoutMs)
    {
        ResolveSingleActionState(actionName, nameof(TriggerAction)).TriggerAction(actionName, timeoutMs);
    }

    public ICache GetCache() => _cache;

    private IServerState ResolveSingleActionState(string actionName, string operationName)
    {
        var matches = _serverStates
            .Where(serverState => serverState.HasAction(actionName))
            .ToArray();

        return matches.Length switch
        {
            0 => throw new ActionDoesNotExistException(
                $"Cannot {operationName} for action '{actionName}' because it is not configured on any server."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"Cannot {operationName} for action '{actionName}' because it is configured on multiple servers. Action names must be unique across the 'Servers' collection.")
        };
    }

    private static InputOutputState ResolveInputOutputState(IEnumerable<IServerState> serverStates)
    {
        var states = serverStates.Select(serverState => serverState.InputOutputState).ToArray();
        if (states.Length == 0)
            return InputOutputState.NoInputOutput;

        if (states.Contains(InputOutputState.BothInputOutput))
            return InputOutputState.BothInputOutput;

        var hasInput = states.Contains(InputOutputState.OnlyInput);
        var hasOutput = states.Contains(InputOutputState.OnlyOutput);

        return (hasInput, hasOutput) switch
        {
            (true, true) => InputOutputState.BothInputOutput,
            (true, false) => InputOutputState.OnlyInput,
            (false, true) => InputOutputState.OnlyOutput,
            _ => InputOutputState.NoInputOutput
        };
    }
}
