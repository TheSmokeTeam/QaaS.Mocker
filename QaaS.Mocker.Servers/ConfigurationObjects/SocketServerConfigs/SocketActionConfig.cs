using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

/// <summary>
/// Describes the action executed for a socket endpoint.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record SocketActionConfig
{
    [Description("The socket server action name identifier")]
    public string? Name { get; set; }

    [Required, Description("The socket server method to perform on the client connection")]
    public SocketMethod? Method { get; set; }

    [RequiredIfAny(nameof(Method), SocketMethod.Broadcast, ErrorMessage = "Invalid socket method"),
     Description("Name of the data-source for the socket server to broadcast data by")]
    public string? DataSourceName { get; set; }

    [Description("The name of the transaction stub to process the data through")]
    public string? TransactionStubName { get; set; }
}
