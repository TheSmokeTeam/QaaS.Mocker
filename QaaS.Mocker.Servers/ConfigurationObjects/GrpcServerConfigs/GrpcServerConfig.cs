using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;

public record GrpcServerConfig
{
    [Required, Range(0, 65535), Description("The port to expose on the grpc server")]
    public int Port { get; set; }

    [Required, MinLength(1), UniquePropertyInEnumerable(nameof(GrpcServiceConfig.ServiceName)),
     Description("The grpc services and rpc actions that are handled by the mocker")]
    public GrpcServiceConfig[] Services { get; set; } = [];

    [Description("To run the server with TLS credentials"), DefaultValue(false)]
    public bool IsSecuredSchema { get; set; } = false;

    [Description("To run the server host as localhost. This is mainly for local testing."), DefaultValue(false)]
    public bool IsLocalhost { get; set; } = false;

    [Description("Transaction stub referred when unknown action is triggered"), DefaultValue(null)]
    public string? NotFoundTransactionStubName { get; set; }

    [Description("Transaction stub referred when internal error in an action is triggered"), DefaultValue(null)]
    public string? InternalErrorTransactionStubName { get; set; }

    [Description("Server certificate path (.pfx) used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePath { get; set; }

    [Description("Server certificate password used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePassword { get; set; }
}
