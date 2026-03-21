using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;

/// <summary>
/// Maps an HTTP method on an endpoint path to the transaction stub that should handle it.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record HttpEndpointActionConfig
{
    [Description("The http endpoint action name identifier")]
    public string? Name { get; set; }

    [Required, Description("The http endpoint action method")]
    public HttpMethod Method { get; set; }

    [Required, Description("The name of the transaction stub to process the request through")]
    public string TransactionStubName { get; set; } = null!;
}
