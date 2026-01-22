namespace QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

/// <summary>
/// Methods that the socket's connection is based on.
/// Collect - awaits for clients to send transactions and collects them into the state.
/// Broadcast - awaits for clients to connect and broadcast them all transactions.
/// </summary>
public enum SocketMethod
{
    Collect,
    Broadcast,
}