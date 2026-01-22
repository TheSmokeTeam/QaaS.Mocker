using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

namespace QaaS.Mocker.Servers.ConfigurationObjects;

public record ServerConfig
{
    [Required, Description("The type of the server protocol to run")]
    public ServerType Type { get; set; }

    [RequiredIfAny(nameof(Type), ServerType.Http),
     Description("'HTTP' server type configuration")]
    public HttpServerConfig? Http { get; set; }

    [RequiredIfAny(nameof(Type), ServerType.Socket),
     Description("Socket streaming server typed configuration")]
    public SocketServerConfig? Socket { get; set; }
}