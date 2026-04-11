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
    public IConfiguration Configuration
    {
        get => ProcessorConfiguration;
        internal set => ProcessorConfiguration = value ?? new ConfigurationBuilder().Build();
    }
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
    /// Sets the name used for the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder Named(string name)
    {
        Name = name;
        return this;
    }

    /// <summary>
    /// Sets the hook implementation name used by the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder HookNamed(string processorName)
    {
        Processor = processorName;
        return this;
    }

    /// <summary>
    /// Adds the supplied data source name to the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder AddDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Append(dataSourceName).ToArray();
        return this;
    }

    /// <summary>
    /// Updates the configured data source name stored on the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder UpdateDataSourceName(string existingValue, string newValue)
    {
        var dataSourceNames = (DataSourceNames ?? []).ToArray();
        var index = Array.IndexOf(dataSourceNames, existingValue);
        if (index < 0)
        {
            return this;
        }

        dataSourceNames[index] = newValue;
        DataSourceNames = dataSourceNames;
        return this;
    }

    /// <summary>
    /// Removes the configured data source name from the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder RemoveDataSourceName(string dataSourceName)
    {
        DataSourceNames = (DataSourceNames ?? []).Where(value => value != dataSourceName).ToArray();
        return this;
    }

    /// <summary>
    /// Removes the configured data source name at the specified index from the current Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder RemoveDataSourceNameAt(int index)
    {
        var dataSourceNames = DataSourceNames ?? [];
        if (index < 0 || index >= dataSourceNames.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        DataSourceNames = dataSourceNames.Where((_, i) => i != index).ToArray();
        return this;
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder Configure(IConfiguration configuration)
    {
        ProcessorConfiguration = configuration ?? new ConfigurationBuilder().Build();
        return this;
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    internal TransactionStubBuilder AddConfiguration(IConfiguration configuration)
    {
        return Configure(configuration);
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder Configure(object configuration)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configuration)));
        ProcessorConfiguration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        return this;
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    internal TransactionStubBuilder AddConfiguration(object configuration)
    {
        return Configure(configuration);
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    internal TransactionStubBuilder Create(IConfiguration configuration)
    {
        return AddConfiguration(configuration);
    }

    /// <summary>
    /// Sets the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    internal TransactionStubBuilder Create(object configuration)
    {
        return AddConfiguration(configuration);
    }

    /// <summary>
    /// Updates the configuration currently stored on the Mocker transaction stub builder instance.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder UpdateConfiguration(object configuration)
    {
        ProcessorConfiguration = (ProcessorConfiguration ?? new ConfigurationBuilder().Build())
            .UpdateConfiguration(configuration);
        return this;
    }

    /// <summary>
    /// Sets how request bodies are deserialized before the stub processor runs.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder WithRequestBodyDeserialization(DeserializeConfig config)
    {
        RequestBodyDeserialization = config;
        return this;
    }

    internal TransactionStubBuilder DeserializeRequestBodyWith(DeserializeConfig config)
    {
        return WithRequestBodyDeserialization(config);
    }

    /// <summary>
    /// Sets how response bodies are serialized after the stub processor runs.
    /// </summary>
    /// <remarks>
    /// Use this method when working with the documented Mocker transaction stub builder API surface in code. The change is stored on the current builder instance and is consumed by later build, validation, or execution steps.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public TransactionStubBuilder WithResponseBodySerialization(SerializeConfig config)
    {
        ResponseBodySerialization = config;
        return this;
    }

    internal TransactionStubBuilder SerializeResponseBodyWith(SerializeConfig config)
    {
        return WithResponseBodySerialization(config);
    }

    /// <summary>
    /// Builds the configured Mocker transaction stub builder output from the current state.
    /// </summary>
    /// <remarks>
    /// Call this after the fluent configuration is complete. The method validates the accumulated state and materializes the runtime or immutable configuration object represented by the builder.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
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
    /// Creates a new Mocker transaction stub builder instance from an existing configuration object.
    /// </summary>
    /// <remarks>
    /// Use this when an existing immutable configuration needs to be brought back into the fluent builder workflow for incremental changes.
    /// </remarks>
    /// <qaas-docs group="Configuration as Code" subgroup="Transaction Stubs" />
    public static TransactionStubBuilder FromConfig(TransactionStubConfig config)
    {
        return new TransactionStubBuilder()
            .Named(config.Name!)
            .HookNamed(config.Processor!)
            .ApplyDataSourceNames(config.DataSourceNames)
            .Configure(config.ProcessorConfiguration)
            .ApplyRequestBodyDeserialization(config.RequestBodyDeserialization)
            .ApplyResponseBodySerialization(config.ResponseBodySerialization);
    }

    private TransactionStubBuilder ApplyDataSourceNames(IEnumerable<string>? dataSourceNames)
    {
        DataSourceNames = dataSourceNames?.ToArray() ?? [];
        return this;
    }

    private TransactionStubBuilder ApplyRequestBodyDeserialization(DeserializeConfig? config)
    {
        RequestBodyDeserialization = config;
        return this;
    }

    private TransactionStubBuilder ApplyResponseBodySerialization(SerializeConfig? config)
    {
        ResponseBodySerialization = config;
        return this;
    }
}
