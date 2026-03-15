using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record HttpEndpointActionConfig
{
    [Description("The http endpoint action name identifier")]
    public string? Name { get; set; }
    
    [Required, Description("The http endpoint action method")]
    public HttpMethod Method { get; set; }
    
    [Required, Description("The name of the transaction stub to process the request through")]
    public string TransactionStubName { get; set; }
}
