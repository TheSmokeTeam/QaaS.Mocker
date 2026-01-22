using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Mocker.Servers.Caches;

namespace QaaS.Mocker.Servers.ServerStates;

/// <summary>
/// TODO docs
/// </summary>
public interface IServerState
{
    public InputOutputState InputOutputState { get; init; }
    public void ChangeActionStub(string actionName, string stubName);
    
    public void TriggerAction(string actionName, int? timeoutMs);
    public ICache GetCache();
}