using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class TransactionStubBuilder
{
    public string? Name { get; private set; }

    public string? Processor { get; private set; }

    public string[] DataSourceNames { get; private set; } = [];

    public IConfiguration ProcessorConfiguration { get; private set; } = new ConfigurationBuilder().Build();

    [Obsolete("Use ProcessorConfiguration instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IConfiguration ProcessorSpecificConfiguration => ProcessorConfiguration;

    public DeserializeConfig? RequestBodyDeserialization { get; private set; }

    public SerializeConfig? ResponseBodySerialization { get; private set; }

    public TransactionStubBuilder Named(string name)
    {
        Name = name;
        return this;
    }

    public TransactionStubBuilder HookNamed(string processorName)
    {
        Processor = processorName;
        return this;
    }

    public TransactionStubBuilder AddDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Append(dataSourceName).ToArray();
        return this;
    }

    public TransactionStubBuilder WithDataSourceNames(IEnumerable<string> dataSourceNames)
    {
        DataSourceNames = dataSourceNames.ToArray();
        return this;
    }

    public TransactionStubBuilder ClearDataSourceNames()
    {
        DataSourceNames = [];
        return this;
    }

    public TransactionStubBuilder Configure(IConfiguration configuration)
    {
        ProcessorConfiguration = configuration;
        return this;
    }

    public TransactionStubBuilder Configure(object configuration)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)));
        ProcessorConfiguration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return this;
    }

    public TransactionStubBuilder DeserializeRequestBodyWith(DeserializeConfig config)
    {
        RequestBodyDeserialization = config;
        return this;
    }

    public TransactionStubBuilder SerializeResponseBodyWith(SerializeConfig config)
    {
        ResponseBodySerialization = config;
        return this;
    }

    public TransactionStubConfig Build()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Stub name is required.");
        if (string.IsNullOrWhiteSpace(Processor))
            throw new InvalidOperationException("Stub processor hook is required.");

        return new TransactionStubConfig
        {
            Name = Name,
            Processor = Processor,
            DataSourceNames = DataSourceNames,
            ProcessorConfiguration = ProcessorConfiguration,
            RequestBodyDeserialization = RequestBodyDeserialization,
            ResponseBodySerialization = ResponseBodySerialization
        };
    }

    public static TransactionStubBuilder FromConfig(TransactionStubConfig config)
    {
        return new TransactionStubBuilder()
            .Named(config.Name!)
            .HookNamed(config.Processor!)
            .WithDataSourceNames(config.DataSourceNames)
            .Configure(config.ProcessorConfiguration)
            .WithRequestBodyDeserialization(config.RequestBodyDeserialization)
            .WithResponseBodySerialization(config.ResponseBodySerialization);
    }

    private TransactionStubBuilder WithRequestBodyDeserialization(DeserializeConfig? config)
    {
        RequestBodyDeserialization = config;
        return this;
    }

    private TransactionStubBuilder WithResponseBodySerialization(SerializeConfig? config)
    {
        ResponseBodySerialization = config;
        return this;
    }
}
