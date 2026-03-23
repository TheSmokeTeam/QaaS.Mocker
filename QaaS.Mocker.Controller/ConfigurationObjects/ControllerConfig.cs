using System.ComponentModel;

namespace QaaS.Mocker.Controller.ConfigurationObjects;

/// <summary>
/// Configures the optional Redis-backed control plane for a mocker runtime.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record ControllerConfig
{
    /// <summary>
    /// Gets or sets the logical server name exposed to control-plane clients.
    /// </summary>
    [Description("The Server name")]
    public string? ServerName { get; set; }

    /// <summary>
    /// Gets or sets the Redis connection settings used by the controller.
    /// </summary>
    [Description("The Server Controller Redis API")]
    internal RedisConfig? Redis { get; set; }

    public RedisConfig? ReadRedis() => Redis;
}
