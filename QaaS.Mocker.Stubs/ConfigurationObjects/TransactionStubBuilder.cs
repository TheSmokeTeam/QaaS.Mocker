using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.Serialization;

namespace QaaS.Mocker.Stubs.ConfigurationObjects;

/// <summary>
/// Provides a fluent API for building <see cref="TransactionStubConfig"/> instances in code.
/// </summary>
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
public class TransactionStubBuilder
{
    /// <summary>
    /// Gets the stub name.
    /// </summary>
    [Description("Name of the transaction stub to reference it by (must be unique).")]
    public string? Name { get; internal set; }

    /// <summary>
    /// Gets the processor hook name used to resolve the stub implementation.
    /// </summary>
    [Description("The name of the transaction processor hook to use.")]
    public string? Processor { get; internal set; }

    /// <summary>
    /// Gets the data source names passed into the stub processor.
    /// </summary>
    [Description("Names of data sources to pass to this stub; they do not need to be defined before the stub.")]
    public string[] DataSourceNames { get; internal set; } = [];

    /// <summary>
    /// Gets the processor-specific configuration.
    /// </summary>
    [Description("Implementation configuration for the processor; the configuration given here is loaded dynamically into the resolved processor.")]
    public IConfiguration ProcessorConfiguration { get; internal set; } = new ConfigurationBuilder().Build();

    /// <summary>
    /// Gets the obsolete alias for <see cref="ProcessorConfiguration"/>.
    /// </summary>
    [Obsolete("Use ProcessorConfiguration instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IConfiguration ProcessorSpecificConfiguration => ProcessorConfiguration;

    /// <summary>
    /// Gets the optional request deserializer configuration.
    /// </summary>
    [Description("Deserializer to use on the request body before invoking the processor.")]
    public DeserializeConfig? RequestBodyDeserialization { get; internal set; }

    /// <summary>
    /// Gets the optional response serializer configuration.
    /// </summary>
    [Description("Serializer to use on the response body after processor execution.")]
    public SerializeConfig? ResponseBodySerialization { get; internal set; }

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
    /// Compatibility alias for <see cref="Configure(Microsoft.Extensions.Configuration.IConfiguration)" /> that matches the configuration CRUD pattern used by other builders.
    /// </summary>
    public TransactionStubBuilder CreateConfiguration(IConfiguration configuration)
    {
        return Configure(configuration);
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
    /// Compatibility alias for <see cref="Configure(object)" /> that matches the configuration CRUD pattern used by other builders.
    /// </summary>
    public TransactionStubBuilder CreateConfiguration(object configuration)
    {
        return Configure(configuration);
    }

    /// <summary>
    /// Compatibility alias for <see cref="CreateConfiguration(Microsoft.Extensions.Configuration.IConfiguration)" />.
    /// </summary>
    public TransactionStubBuilder Create(IConfiguration configuration)
    {
        return CreateConfiguration(configuration);
    }

    /// <summary>
    /// Compatibility alias for <see cref="CreateConfiguration(object)" />.
    /// </summary>
    public TransactionStubBuilder Create(object configuration)
    {
        return CreateConfiguration(configuration);
    }

    /// <summary>
    /// Returns the currently configured processor configuration.
    /// </summary>
    public IConfiguration ReadConfiguration()
    {
        return ProcessorConfiguration;
    }

    /// <summary>
    /// Merges the provided configuration object into the current processor configuration.
    /// </summary>
    public TransactionStubBuilder UpdateConfiguration(object configuration)
    {
        ProcessorConfiguration = ProcessorConfiguration.UpdateConfiguration(configuration);
        return this;
    }

    /// <summary>
    /// Clears the configured processor configuration.
    /// </summary>
    public TransactionStubBuilder DeleteConfiguration()
    {
        ProcessorConfiguration = new ConfigurationBuilder().Build();
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
