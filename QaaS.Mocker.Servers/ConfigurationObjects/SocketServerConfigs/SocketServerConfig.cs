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
     UniqueActionNameInAllEndpoints, BroadcastOverUdpNotSupported, SocketTypeMatchesProtocol,
     // TODO - merge attribute and maybe endpoint typed records
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

/// <summary>
/// Rejects UDP broadcast endpoints up front because the current socket runtime has no remote
/// destination to send to for that mode.
/// </summary>
internal class BroadcastOverUdpNotSupportedAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable<SocketEndpointConfig> endpoints)
            return ValidationResult.Success;

        var invalidPorts = endpoints
            .Where(endpoint => endpoint.ProtocolType == System.Net.Sockets.ProtocolType.Udp &&
                               endpoint.Action?.Method == SocketMethod.Broadcast)
            .Select(endpoint => endpoint.Port)
            .Where(port => port.HasValue)
            .Select(port => port!.Value)
            .ToArray();

        return invalidPorts.Length > 0
            ? new ValidationResult("Broadcast socket endpoints do not support UDP. Invalid ports: " +
                                   string.Join(", ", invalidPorts))
            : ValidationResult.Success;
    }
}

/// <summary>
/// Rejects protocol/socket-type combinations that would otherwise fail later with opaque
/// platform socket exceptions during server construction.
/// </summary>
internal class SocketTypeMatchesProtocolAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable<SocketEndpointConfig> endpoints)
            return ValidationResult.Success;

        var invalidPorts = endpoints
            .Where(endpoint =>
                endpoint.ProtocolType == System.Net.Sockets.ProtocolType.Udp && endpoint.SocketType != System.Net.Sockets.SocketType.Dgram ||
                endpoint.ProtocolType == System.Net.Sockets.ProtocolType.Tcp && endpoint.SocketType != System.Net.Sockets.SocketType.Stream)
            .Select(endpoint => endpoint.Port)
            .Where(port => port.HasValue)
            .Select(port => port!.Value)
            .ToArray();

        return invalidPorts.Length > 0
            ? new ValidationResult("Socket endpoint protocol and socket type must match. Invalid ports: " +
                                   string.Join(", ", invalidPorts))
            : ValidationResult.Success;
    }
}
