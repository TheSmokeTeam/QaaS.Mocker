using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

/// <summary>
/// Describes a named transaction stub and the processor metadata required to execute it.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public record TransactionStubConfig : IYamlConvertible
{
    /// <summary>
    /// Gets or sets the stub name.
    /// </summary>
    [Required, Description("Name of data source to reference it by (must be unique)")]
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the transaction processor hook name.
    /// </summary>
    [Required, Description("The name of the processor to use")]
    public string? Processor { get; set; }
    
    /// <summary>
    /// Gets or sets the data source names passed into the processor.
    /// </summary>
    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description("Names of data sources to pass to this data source for usage, those data sources dont have to be" +
                 " defined before this data source.")]
    public string[] DataSourceNames { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the dynamic processor configuration.
    /// </summary>
    [Description("Implementation configuration for the processor, " +
                 "the configuration given here is loaded into the provided processor dynamically.")]
    internal IConfiguration ProcessorConfiguration { get; set; } = new ConfigurationBuilder().Build();

    [Obsolete("Use ProcessorConfiguration instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal IConfiguration ProcessorSpecificConfiguration
    {
        get => ProcessorConfiguration;
        set => ProcessorConfiguration = value ?? new ConfigurationBuilder().Build();
    }
    
    /// <summary>
    /// Gets or sets the optional request-body deserialization behavior.
    /// </summary>
    [Description("Deserialize to use on the request body"), DefaultValue(null)]
    internal DeserializeConfig? RequestBodyDeserialization { get; set; } = null;
    
    /// <summary>
    /// Gets or sets the optional response-body serialization behavior.
    /// </summary>
    [Description("Serialize to use on the response body"), DefaultValue(null)]
    internal SerializeConfig? ResponseBodySerialization { get; set; } = null;

    public IConfiguration ReadProcessorConfiguration() => ProcessorConfiguration;

    public DeserializeConfig? ReadRequestBodyDeserialization() => RequestBodyDeserialization;

    public SerializeConfig? ReadResponseBodySerialization() => ResponseBodySerialization;

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
