using System.ComponentModel;

namespace QaaS.Mocker.Controller.ConfigurationObjects;

public record ControllerConfig
{
    [Description("The Server name")]
    public string? ServerName { get; set; } = null;

    [Description("The Server Controller Redis API")]
    public RedisConfig? Redis { get; set; } = null;
}