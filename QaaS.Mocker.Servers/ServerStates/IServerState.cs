using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Mocker.Servers.Caches;

namespace QaaS.Mocker.Servers.ServerStates;

/// <summary>
/// Defines mutable runtime behavior for a mock server implementation.
/// </summary>
public interface IServerState
{
    /// <summary>
    /// Describes whether the server produces input, output, or both for controller consumption.
    /// </summary>
    public InputOutputState InputOutputState { get; init; }

    /// <summary>
    /// Rebinds an action to a different transaction stub at runtime.
    /// </summary>
    public void ChangeActionStub(string actionName, string stubName);
    
    /// <summary>
    /// Temporarily enables or triggers the given action.
    /// </summary>
    public void TriggerAction(string actionName, int? timeoutMs);

    /// <summary>
    /// Returns the cache that stores collected input/output data.
    /// </summary>
    public ICache GetCache();
}
