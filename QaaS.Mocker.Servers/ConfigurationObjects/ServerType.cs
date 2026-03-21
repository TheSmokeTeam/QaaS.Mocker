namespace QaaS.Mocker.Servers.ConfigurationObjects;

/// <summary>
/// Supported mocker transport implementations.
/// </summary>
public enum ServerType
{
    Unknown = 0,
    Http = 1,
    Grpc = 2,
    Socket = 3
}
