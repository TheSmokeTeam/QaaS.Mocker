using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;

/// <summary>
/// Describes a gRPC server endpoint and the services it exposes.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record GrpcServerConfig : IValidatableObject
{
    [Required, Range(0, 65535), Description("The port to expose on the grpc server")]
    public int Port { get; set; }

    [Required, MinLength(1), UniquePropertyInEnumerable(nameof(GrpcServiceConfig.ServiceName)),
     Description("The grpc services and rpc actions that are handled by the mocker")]
    public GrpcServiceConfig[] Services { get; set; } = [];

    [Description("To run the server with TLS credentials"), DefaultValue(false)]
    public bool IsSecuredSchema { get; set; }

    [Description("To run the server host as localhost. This is mainly for local testing."), DefaultValue(false)]
    public bool IsLocalhost { get; set; }

    [Description("Transaction stub referred when unknown action is triggered"), DefaultValue(null)]
    public string? NotFoundTransactionStubName { get; set; }

    [Description("Transaction stub referred when internal error in an action is triggered"), DefaultValue(null)]
    public string? InternalErrorTransactionStubName { get; set; }

    [Description("Server certificate path (.pfx) used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePath { get; set; }

    [Description("Server certificate password used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePassword { get; set; }

    /// <summary>
    /// Validates TLS-specific settings before server startup.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsSecuredSchema)
            yield break;

        if (string.IsNullOrWhiteSpace(CertificatePath))
        {
            yield return new ValidationResult(
                "Server.Grpc.CertificatePath is required when Server.Grpc.IsSecuredSchema is true.",
                [nameof(CertificatePath)]);
            yield break;
        }

        var resolvedCertificatePath = ResolveCertificatePath(CertificatePath);
        if (!File.Exists(resolvedCertificatePath))
        {
            yield return new ValidationResult(
                $"Server.Grpc.CertificatePath '{CertificatePath}' was not found. Relative paths are resolved from the current working directory '{Environment.CurrentDirectory}'.",
                [nameof(CertificatePath)]);
        }
    }

    private static string ResolveCertificatePath(string certificatePath)
    {
        return Path.IsPathRooted(certificatePath)
            ? certificatePath
            : Path.Combine(Environment.CurrentDirectory, certificatePath);
    }
}
