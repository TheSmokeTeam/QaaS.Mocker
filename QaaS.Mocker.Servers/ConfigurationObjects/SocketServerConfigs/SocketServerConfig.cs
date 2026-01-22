using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;

public record SocketServerConfig
{
    [DefaultValue("0.0.0.0"), Description("Subnet ipv4 mask to bind socket client's connection to")]
    public string BindingIpAddress { get; set; } = "0.0.0.0";

    [Description(
         "The socket's connection acceptance value used for the semaphore (Multiplied with local processor count)"),
     DefaultValue(8)]
    public int ConnectionAcceptanceValue { get; set; } = 8;

    [Required, UniquePropertyInEnumerable(nameof(SocketEndpointConfig.Port)), MinLength(1),
     UniqueActionNameInAllEndpoints, // TODO - merge attribute and maybe endpoint typed records
     Description("All socket endpoint-implementation which handled by the socket server")]
    public SocketEndpointConfig[]? Endpoints { get; set; }
}

internal class UniqueActionNameInAllEndpointsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable of SocketEndpointConfig or if its null - validation is automatically successful 
        if (value is not IEnumerable<SocketEndpointConfig>) return ValidationResult.Success;
        var configuration = (SocketServerConfig)validationContext.ObjectInstance;
        if (configuration.Endpoints == null) return ValidationResult.Success;

        var actionNames = new List<string>();

        foreach (var endpoint in configuration.Endpoints)
            if (endpoint.Action?.Name != null)
                actionNames.Add(endpoint.Action.Name.ToLower());

        var actionNamesDuplicates = actionNames.GroupBy(id => id)
            .Where(idGroup => idGroup.Count() > 1)
            .Select(idGroup => idGroup.Key)
            .ToArray();


        return actionNamesDuplicates.Length > 0
            ? new ValidationResult($"Duplication in the following Action Names: " +
                                   $"{string.Join(", ", actionNamesDuplicates)}")
            : ValidationResult.Success;
    }
}