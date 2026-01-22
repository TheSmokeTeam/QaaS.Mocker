using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations.ConfigurationBindingUtils;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

public record TransactionStubConfig : IYamlConvertible
{
    [Required, Description("Name of data source to reference it by (must be unique)")]
    public string? Name { get; set; }
    
    [Required, Description("The name of the processor to use")]
    public string? Processor { get; set; }
    
    [EnumerablePropertyDoesNotContainAnotherPropertyValue(nameof(Name)),
     Description("Names of data sources to pass to this data source for usage, those data sources dont have to be" +
                 " defined before this data source.")]
    public string[] DataSourceNames { get; set; } = Array.Empty<string>();

    [Description("Implementation specific configuration for the processor, " +
                 "the configuration given here is loaded into the provided processor dynamically.")]
    public IConfiguration ProcessorSpecificConfiguration { get; set; } = new ConfigurationBuilder().Build();
    
    [Description("Deserialize to use on the request body"), DefaultValue(null)]
    public DeserializeConfig? RequestBodyDeserialization { get; set; } = null;
    
    [Description("Serialize to use on the response body"), DefaultValue(null)]
    public SerializeConfig? ResponseBodySerialization { get; set; } = null;

    public void Read(IParser parser, Type expectedType, ObjectDeserializer nestedObjectDeserializer)
    {
        throw new NotSupportedException($"{nameof(Read)} doesn't support custom" +
                                        $" deserialization from Yaml for {nameof(TransactionStubConfig)}");
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        var generatorSpecificConfiguration = ProcessorSpecificConfiguration
            .GetDictionaryFromConfiguration();
        
        nestedObjectSerializer(new {
            Name,
            Processor,
            DataSourceNames,
            RequestBodyDeserialization,
            ResponseBodySerialization,
            GeneratorSpecificConfiguration = generatorSpecificConfiguration
        });
    }
}