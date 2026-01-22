using QaaS.Mocker.Servers.ServerStates;

namespace QaaS.Mocker.Servers.Servers;

/// <summary>
/// TODO docs
/// </summary>
public interface IServer
{
    public IServerState State { get; init; }
    
    /// <summary>
    /// Starts the server.
    /// </summary>
    public void Start();
}