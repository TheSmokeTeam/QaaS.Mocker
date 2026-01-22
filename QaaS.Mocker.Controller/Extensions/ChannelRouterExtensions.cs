namespace QaaS.Mocker.Controller.Extensions;

public static class ChannelRouterExtensions
{
    public static string SubPingsChannel() => "runner:mocker:pings";
    public static string SubCommandsChannel(string serverName) => $"runner:mocker:commands:{serverName}";
    public static string PubAcks() => $"mocker:runner:acks";
}