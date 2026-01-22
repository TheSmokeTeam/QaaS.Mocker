using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using QaaS.Framework.Configurations.CustomValidationAttributes;

namespace QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;

public record HttpEndpointConfig
{
    [Required, UniquePropertyInEnumerable(nameof(HttpEndpointActionConfig.Method)),
     Description("The http endpoint method actions")]
    public HttpEndpointActionConfig[] Actions { get; set; }
    
    [Required, Description("The http endpoint Path")]
    public string Path { get; set; }

    public string FixedPath
    {
        get
        {
            var path = Path.ToLower();
            if (path.Length != 1) path = path.TrimEnd(Slash);
            return path;
        }
    } 
    
    private static readonly Regex PathPattern =
        new(@"^\/$|^\/([\w\-]+\/)*([\w\-]+|{\w+})(\/[\w\-]+|\/{\w+})*(\/)?$");
    public bool IsPathValid() => PathPattern.IsMatch(FixedPath);

    
    private static readonly Regex ParameterSchemaInPathPattern = new(@"{(\w+)}");
    private const string ParameterSectionInPathRegexPattern = @"[\w\-]+";
    private const int IndexOneRegexMatchGroup = 1;
    private const char Slash = '/';
    private const string PlaceholderSectionName = "placeholder";
    
    private string[] RetrieveSegmentsFromPath()
    {
        if (!IsPathValid()) 
            throw new NotSupportedException($"Can't process invalid Path '{FixedPath}'");
      
        var parameterNames = new HashSet<string>();
        var parameterRegexMatches = ParameterSchemaInPathPattern.Matches(FixedPath);

        foreach (Match parameterRegexMatch in parameterRegexMatches)
        {
            var parameterName = parameterRegexMatch.Groups[IndexOneRegexMatchGroup].Value;
            if (!parameterNames.Add(parameterName))
                throw new ArgumentException($"Multiple '{parameterName}' parameters in Path '{FixedPath}'");
        }

        var segments = FixedPath.Split(Slash);
        if (!segments.Select(segment => ParameterSchemaInPathPattern.Matches(segment))
                .All(parameterMatches => parameterMatches.Count <= 1))
            throw new ArgumentException($"Multiple parameters in the same section in Path '{FixedPath}'");

        return segments;
    }
    
    public Regex GeneratePathRegex()
    {
        var segments = RetrieveSegmentsFromPath();

        var pathBuilder = (from segment in segments
            where !string.IsNullOrEmpty(segment)
            let match = ParameterSchemaInPathPattern.Match(segment)
            select match.Success
                ? $"(?<{match.Groups[IndexOneRegexMatchGroup].Value}>{ParameterSectionInPathRegexPattern})"
                : Regex.Escape(segment)).ToList();
        
        return new Regex("^/" + string.Join("/", pathBuilder) + "$");
    }
    
    public string GenerateDummyPath()
    {
        var segments = RetrieveSegmentsFromPath();

        var pathBuilder = (from segment in segments
            where !string.IsNullOrEmpty(segment)
            select ParameterSchemaInPathPattern.IsMatch(segment)
                ? PlaceholderSectionName
                : Regex.Escape(segment)).ToList();
        
        return "/" + string.Join("/", pathBuilder);
    }
}