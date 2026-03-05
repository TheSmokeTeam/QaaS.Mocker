using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;

public record HttpServerConfig
{
    [Required, Range(0, 65535), Description("The port to expose on the http server")]
    public int Port { get; set; }
    
    [UniquePropertyInEnumerable(nameof(HttpEndpointConfig.Path)), 
     ValidAndUniquePathRegexEndpoints, UniqueActionNameEndpoints,
     Description("All endpints which handled by the http server")]
    public HttpEndpointConfig[]? Endpoints { get; set; }

    [Description("To run the server with a secured schema. This is for mainly for local testing."), DefaultValue(false)] 
    public bool IsSecuredSchema { get; set; } = false;

    [Description("Server certificate path (.pfx) used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePath { get; set; }

    [Description("Server certificate password used when IsSecuredSchema is true"), DefaultValue(null)]
    public string? CertificatePassword { get; set; }
    
    [Description("To run the server host as localhost. This is for mainly for local testing."), DefaultValue(false)] 
    public bool IsLocalhost { get; set; } = false;

    [Description("Transaction stub referred when unknown action is triggered"), DefaultValue(null)] 
    public string? NotFoundTransactionStubName { get; set; } = null;
    
    [Description("Transaction stub referred when internal error in an action is triggered"), DefaultValue(null)] 
    public string? InternalErrorTransactionStubName { get; set; } = null;

    [Description("The http connection acceptance value used for the semaphore (Multiplied with local processor count)"),
     DefaultValue(128)]
    public int ConnectionAcceptanceValue { get; set; } = 128;
}

internal class ValidAndUniquePathRegexEndpointsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable of HttpEndpointConfig or if its null - validation is automatically successful 
        if (value is not IEnumerable<HttpEndpointConfig> ) return ValidationResult.Success;
        var configuration = (HttpServerConfig) validationContext.ObjectInstance;
        if (configuration.Endpoints == null) return ValidationResult.Success;

        var invalidEndpointPaths = (
            from endpoint in configuration.Endpoints
            where !endpoint.IsPathValid()
            select endpoint.Path
        ).ToList();

        if (invalidEndpointPaths.Count > 0)
            return new ValidationResult($"The following Endpoint Paths are not valid:\n\t- " +
                                        $"{string.Join("\n\t- ", invalidEndpointPaths)}");

        var endpointPathsToRegexMapping = new Dictionary<string, Regex>();
        var endpointPathsToDummyMapping = new Dictionary<string, string>();
        
        foreach (var endpoint in configuration.Endpoints)
        {
            endpointPathsToRegexMapping[endpoint.Path] = endpoint.GeneratePathRegex();
            endpointPathsToDummyMapping[endpoint.Path] = endpoint.GenerateDummyPath();
        }

        var conflictingEndpointPaths = new List<KeyValuePair<string, string>>();
        
        for (var endpointIndex = 0; endpointIndex < configuration.Endpoints.Length; endpointIndex++)
        {
            var path = configuration.Endpoints[endpointIndex].Path;
            for (var otherEndpointIndex = endpointIndex + 1; otherEndpointIndex < configuration.Endpoints.Length; otherEndpointIndex++)
            {
                var otherPath = configuration.Endpoints[otherEndpointIndex].Path;
                var pathsConflict =
                    endpointPathsToRegexMapping[otherPath].IsMatch(endpointPathsToDummyMapping[path]) ||
                    endpointPathsToRegexMapping[path].IsMatch(endpointPathsToDummyMapping[otherPath]);

                if (pathsConflict)
                    conflictingEndpointPaths.Add(new KeyValuePair<string, string>(path, otherPath));
            }
        }
        
        var conflictingEndpointPathsPairs = conflictingEndpointPaths
            .Select(pair => $"{pair.Key} - {pair.Value}").ToList();
        
        return conflictingEndpointPathsPairs.Count > 0 ? 
            new ValidationResult($"The following Endpoint Paths are conflicting:\n\t- " +
                                 $"{string.Join("\n\t- ", conflictingEndpointPathsPairs)}")
            : ValidationResult.Success;
    }
}


internal class UniqueActionNameEndpointsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // If item is not an enumerable of HttpEndpointConfig or if its null - validation is automatically successful 
        if (value is not IEnumerable<HttpEndpointConfig> ) return ValidationResult.Success;
        var configuration = (HttpServerConfig) validationContext.ObjectInstance;
        if (configuration.Endpoints == null) return ValidationResult.Success;

        var actionNames = new List<string>();
        
        foreach (var endpoint in configuration.Endpoints)
            foreach (var action in endpoint.Actions)
                if (action.Name != null) actionNames.Add(action.Name.ToLower()); 
        
        var actionNamesDuplicates = actionNames.GroupBy(id => id)
            .Where(idGroup => idGroup.Count() > 1)
            .Select(idGroup => idGroup.Key)
            .ToArray();

        
        return actionNamesDuplicates.Length > 0 ? 
            new ValidationResult($"Duplication in the following Action Ids: " +
                                 $"{string.Join(", ", actionNamesDuplicates)}")
            : ValidationResult.Success;
    }
}
