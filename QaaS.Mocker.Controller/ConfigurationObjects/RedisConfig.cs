using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.ConfigurationObjects;

/// <summary>
/// Describes the Redis connection used by the mocker controller handlers.
/// </summary>
[ExcludeFromCodeCoverage]
public record RedisConfig
{
    /// <summary>
    /// Gets or sets the Redis host and port.
    /// </summary>
    [Description("Redis hostname (should contain the port too for example: - 'host1:8080')")]
    public string Host { get; set; } = null!;

    [Description("User for the redis server"), DefaultValue(null)]
    public string? Username { get; set; }

    [Description("Password for the redis server"), DefaultValue(null)]
    public string? Password { get; set; }

    [Description("If true, connect will not create connection while no servers are available"), DefaultValue(true)]
    public bool AbortOnConnectFail { get; set; } = true;

    [Description("The number of times to repeat connect attempts during initial connect"), DefaultValue(3)]
    public int ConnectRetry { get; set; } = 3;

    [Description("Identification for the connection within redis"), DefaultValue(null)]
    public string? ClientName { get; set; }

    [Description("Time(ms) to allow for asynchronous operations"), DefaultValue(5000)]
    public int AsyncTimeout { get; set; } = 5000;

    [Description("Specifies that SSL encryption should be used"), DefaultValue(false)]
    public bool Ssl { get; set; }

    [Description("Enforces a preticular SSL host identity on the server's certificate"), DefaultValue(null)]
    public string? SslHost { get; set; }

    [Description("Time (seconds) at which to send a message to help keep alive"), DefaultValue(60)]
    public int KeepAlive { get; set; } = 60;

    [Description("Redis database to use"), DefaultValue(0)]
    public int RedisDataBase { get; set; }

    /// <summary>
    /// Creates the StackExchange.Redis connection options expected by the controller factory.
    /// </summary>
    public ConfigurationOptions CreateRedisConfigurationOptions()
    {
        return new ConfigurationOptions
        {
            EndPoints = { Host },
            User = Username,
            Password = Password,
            AbortOnConnectFail = AbortOnConnectFail,
            ConnectRetry = ConnectRetry,
            KeepAlive = KeepAlive,
            ClientName = ClientName,
            AsyncTimeout = AsyncTimeout,
            Ssl = Ssl,
            SslHost = SslHost
        };
    }
}
