using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
/// <summary>
/// Provides a fluent API for building <see cref="TransactionStubConfig"/> instances in code.
/// </summary>
public class TransactionStubBuilder
{
    /// <summary>
    /// Gets the stub name.
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// Gets the processor hook name used to resolve the stub implementation.
    /// </summary>
    public string? Processor { get; private set; }

    /// <summary>
    /// Gets the data source names passed into the stub processor.
    /// </summary>
    public string[] DataSourceNames { get; private set; } = [];

    /// <summary>
    /// Gets the processor-specific configuration.
    /// </summary>
    public IConfiguration ProcessorConfiguration { get; private set; } = new ConfigurationBuilder().Build();

    [Obsolete("Use ProcessorConfiguration instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    /// <summary>
    /// Gets the obsolete alias for <see cref="ProcessorConfiguration"/>.
    /// </summary>
    public IConfiguration ProcessorSpecificConfiguration => ProcessorConfiguration;

    /// <summary>
    /// Gets the optional request deserializer configuration.
    /// </summary>
    public DeserializeConfig? RequestBodyDeserialization { get; private set; }

    /// <summary>
    /// Gets the optional response serializer configuration.
    /// </summary>
    public SerializeConfig? ResponseBodySerialization { get; private set; }

    /// <summary>
    /// Sets the stub name.
    /// </summary>
    public TransactionStubBuilder Named(string name)
    {
        Name = name;
        return this;
    }

    /// <summary>
    /// Sets the processor hook name.
    /// </summary>
    public TransactionStubBuilder HookNamed(string processorName)
    {
        Processor = processorName;
        return this;
    }

    /// <summary>
    /// Adds a single data source name to the stub.
    /// </summary>
    public TransactionStubBuilder AddDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Append(dataSourceName).ToArray();
        return this;
    }

    /// <summary>
    /// Replaces the data source name list.
    /// </summary>
    public TransactionStubBuilder WithDataSourceNames(IEnumerable<string> dataSourceNames)
    {
        DataSourceNames = dataSourceNames.ToArray();
        return this;
    }

    /// <summary>
    /// Removes every configured data source name.
    /// </summary>
    public TransactionStubBuilder ClearDataSourceNames()
    {
        DataSourceNames = [];
        return this;
    }

    /// <summary>
    /// Replaces the processor configuration with an existing configuration object.
    /// </summary>
    public TransactionStubBuilder Configure(IConfiguration configuration)
    {
        ProcessorConfiguration = configuration;
        return this;
    }

    /// <summary>
    /// Serializes an object into JSON-backed processor configuration.
    /// </summary>
    public TransactionStubBuilder Configure(object configuration)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)));
        ProcessorConfiguration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return this;
    }

    /// <summary>
    /// Configures how request bodies are deserialized before processor execution.
    /// </summary>
    public TransactionStubBuilder DeserializeRequestBodyWith(DeserializeConfig config)
    {
        RequestBodyDeserialization = config;
        return this;
    }

    /// <summary>
    /// Configures how response bodies are serialized after processor execution.
    /// </summary>
    public TransactionStubBuilder SerializeResponseBodyWith(SerializeConfig config)
    {
        ResponseBodySerialization = config;
        return this;
    }

    /// <summary>
    /// Materializes the fluent builder into an immutable configuration record.
    /// </summary>
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

    /// <summary>
    /// Creates a fluent builder from an existing configuration record.
    /// </summary>
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
