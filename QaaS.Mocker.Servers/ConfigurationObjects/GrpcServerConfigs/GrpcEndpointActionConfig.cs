using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;

/// <summary>
/// Maps a gRPC RPC method to the transaction stub that should handle it.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record GrpcEndpointActionConfig
{
    [Description("The grpc action name identifier")]
    public string? Name { get; set; }

    [Required, Description("The rpc method name")]
    public string RpcName { get; set; } = null!;

    [Required, Description("The transaction stub used for this rpc method")]
    public string TransactionStubName { get; set; } = null!;
}
