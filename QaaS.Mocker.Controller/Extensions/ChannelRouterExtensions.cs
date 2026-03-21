namespace QaaS.Mocker.Controller.Extensions;

/// <summary>
/// Centralizes the legacy Redis channel naming scheme used by controller integrations.
/// </summary>
public static class ChannelRouterExtensions
{
    /// <summary>
    /// Gets the shared subscription channel used for ping requests.
    /// </summary>
    public static string SubPingsChannel() => "runner:mocker:pings";

    /// <summary>
    /// Gets the subscription channel used for command requests for a given server.
    /// </summary>
    public static string SubCommandsChannel(string serverName) => $"runner:mocker:commands:{serverName}";

    /// <summary>
    /// Gets the publication channel used for command acknowledgements.
    /// </summary>
    public static string PubAcks() => "mocker:runner:acks";
}
