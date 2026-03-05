using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;

public record GrpcServiceConfig
{
    [Required, Description("The proto service name")]
    public string ServiceName { get; set; } = null!;

    [Required, Description("The namespace containing generated grpc proto classes")]
    public string ProtoNamespace { get; set; } = null!;

    [Required, Description("The assembly containing generated grpc proto classes")]
    public string AssemblyName { get; set; } = null!;

    [Required, MinLength(1), UniquePropertyInEnumerable(nameof(GrpcEndpointActionConfig.RpcName)),
     Description("The rpc actions for this grpc service")]
    public GrpcEndpointActionConfig[] Actions { get; set; } = [];
}
