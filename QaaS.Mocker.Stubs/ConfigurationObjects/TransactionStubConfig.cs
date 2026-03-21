using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
/// <summary>
/// Describes a named transaction stub and the processor metadata required to execute it.
/// </summary>
public record TransactionStubConfig : IYamlConvertible
{
    [Required, Description("Name of data source to reference it by (must be unique)")]
    /// <summary>
    /// Gets or sets the stub name.
    /// </summary>
    public string? Name { get; set; }
    
    [Required, Description("The name of the processor to use")]
    /// <summary>
    /// Gets or sets the transaction processor hook name.
    /// </summary>
    public string? Processor { get; set; }
    
    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description("Names of data sources to pass to this data source for usage, those data sources dont have to be" +
                 " defined before this data source.")]
    /// <summary>
    /// Gets or sets the data source names passed into the processor.
    /// </summary>
    public string[] DataSourceNames { get; set; } = Array.Empty<string>();

    [Description("Implementation configuration for the processor, " +
                 "the configuration given here is loaded into the provided processor dynamically.")]
    /// <summary>
    /// Gets or sets the dynamic processor configuration.
    /// </summary>
    public IConfiguration ProcessorConfiguration { get; set; } = new ConfigurationBuilder().Build();

    [Obsolete("Use ProcessorConfiguration instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal IConfiguration ProcessorSpecificConfiguration
    {
        get => ProcessorConfiguration;
        set => ProcessorConfiguration = value ?? new ConfigurationBuilder().Build();
    }
    
    [Description("Deserialize to use on the request body"), DefaultValue(null)]
    /// <summary>
    /// Gets or sets the optional request-body deserialization behavior.
    /// </summary>
    public DeserializeConfig? RequestBodyDeserialization { get; set; } = null;
    
    [Description("Serialize to use on the response body"), DefaultValue(null)]
    /// <summary>
    /// Gets or sets the optional response-body serialization behavior.
    /// </summary>
    public SerializeConfig? ResponseBodySerialization { get; set; } = null;

    /// <summary>
    /// Custom YAML deserialization is intentionally not supported for this type.
    /// </summary>
    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        throw new NotSupportedException($"{nameof(Read)} doesn't support custom" +
                                        $" deserialization from Yaml for {nameof(TransactionStubConfig)}");
    }

    /// <summary>
    /// Writes the configuration as YAML using a plain dictionary projection for the processor settings.
    /// </summary>
    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        var processorConfiguration = ProcessorConfiguration
            .GetDictionaryFromConfiguration();
        
        nestedObjectSerializer(new {
            Name,
            Processor,
            DataSourceNames,
            ProcessorConfiguration = processorConfiguration,
            RequestBodyDeserialization,
            ResponseBodySerialization
        });
    }
}
