using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

namespace QaaS.Mocker.Servers.ConfigurationObjects;

/// <summary>
/// Wraps the protocol-specific configuration for one configured server instance.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record ServerConfig : IValidatableObject
{
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Text.Json.Serialization.JsonIgnore]
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
    /// Resolves the configured server protocol from the configured transport section.
    /// </summary>
    public ServerType ResolveType()
    {
        var configuredTypes = GetConfiguredServerTypes().Distinct().ToArray();
        if (configuredTypes.Length == 1)
        {
            if (Type != ServerType.Unknown && Type != configuredTypes[0])
            {
                throw new InvalidOperationException(
                    $"Legacy Server.Type value '{Type}' does not match the configured transport section '{configuredTypes[0]}'.");
            }

            return configuredTypes[0];
        }

        if (configuredTypes.Length == 0 && Type != ServerType.Unknown)
        {
            return Type;
        }

        throw new InvalidOperationException("Server must configure exactly one of Http, Grpc, or Socket.");
    }

    /// <summary>
    /// Validates that exactly one transport section is configured and that any legacy discriminator matches it.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var configuredTypes = GetConfiguredServerTypes().Distinct().ToArray();
        if (configuredTypes.Length == 0)
        {
            yield return new ValidationResult(
                "Server must configure exactly one of: Http, Grpc, Socket.",
                [nameof(Http), nameof(Grpc), nameof(Socket)]);
            yield break;
        }

        if (configuredTypes.Length > 1)
        {
            yield return new ValidationResult(
                "Server can configure only one of: Http, Grpc, Socket.",
                [nameof(Http), nameof(Grpc), nameof(Socket)]);
            yield break;
        }

        if (Type != ServerType.Unknown && Type != configuredTypes[0])
        {
            yield return new ValidationResult(
                $"Legacy Server.Type value '{Type}' does not match the configured transport section '{configuredTypes[0]}'.",
                [nameof(Http), nameof(Grpc), nameof(Socket)]);
        }
    }

    private IEnumerable<ServerType> GetConfiguredServerTypes()
    {
        if (Http != null)
            yield return ServerType.Http;
        if (Grpc != null)
            yield return ServerType.Grpc;
        if (Socket != null)
            yield return ServerType.Socket;
    }
}
