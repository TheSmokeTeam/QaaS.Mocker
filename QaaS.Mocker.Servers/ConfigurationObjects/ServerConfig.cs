using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

namespace QaaS.Mocker.Servers.ConfigurationObjects;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record ServerConfig : IValidatableObject
{
    [Required, Description("The type of the server protocol to run")]
    public ServerType Type { get; set; }

    [RequiredIfAny(nameof(Type), ServerType.Http),
     Description("'HTTP' server type configuration")]
    public HttpServerConfig? Http { get; set; }

    [RequiredIfAny(nameof(Type), ServerType.Grpc),
     Description("'gRPC' server type configuration")]
    public GrpcServerConfig? Grpc { get; set; }

    [RequiredIfAny(nameof(Type), ServerType.Socket),
     Description("Socket streaming server typed configuration")]
    public SocketServerConfig? Socket { get; set; }

    /// <summary>
    /// Rejects the unset enum default so template output and validation remain explicit.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Type == ServerType.Unknown)
        {
            yield return new ValidationResult(
                "Server.Type is required and must be one of: Http, Grpc, Socket.",
                [nameof(Type)]);
        }
    }
}
