using QaaS.Mocker.Servers.ServerStates;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// Represents a concrete mock server transport (HTTP, gRPC, or Socket).
/// </summary>
public interface IServer
{
    /// <summary>
    /// Gets the state engine used by this server to resolve actions and stubs.
    /// </summary>
    public IServerState State { get; init; }
    
    /// <summary>
    /// Starts accepting and processing incoming traffic.
    /// </summary>
    public void Start();
}
