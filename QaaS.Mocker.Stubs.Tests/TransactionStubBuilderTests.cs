using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.Configurations;
using QaaS.Framework.Serialization;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using YamlDotNet.Serialization;

namespace QaaS.Mocker.Stubs.Tests;

[TestFixture]
public class TransactionStubBuilderTests
{
    [Test]
    public void Build_WithObjectConfiguration_LoadsProcessorConfiguration()
    {
        var config = new { Prefix = "value", Retries = 3 };

        var built = new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("MyProcessor")
            .Configure(config)
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(built.Name, Is.EqualTo("StubA"));
            Assert.That(built.Processor, Is.EqualTo("MyProcessor"));
            Assert.That(built.ProcessorConfiguration["Prefix"], Is.EqualTo("value"));
            Assert.That(built.ProcessorConfiguration["Retries"], Is.EqualTo("3"));
        });
    }

    [Test]
    public void Build_WithIConfiguration_UsesGivenConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Nested:Enabled"] = "true"
            })
            .Build();

        var built = new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("MyProcessor")
            .Configure(configuration)
            .Build();

        Assert.That(built.ProcessorConfiguration["Nested:Enabled"], Is.EqualTo("true"));
    }

    [Test]
    public void Build_SerializesProcessorConfigurationWithUpdatedKey()
    {
        var built = new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("MyProcessor")
            .Configure(new { Prefix = "value" })
            .Build();

        var yaml = new SerializerBuilder().Build().Serialize(built);

        Assert.Multiple(() =>
        {
            Assert.That(yaml, Does.Contain("ProcessorConfiguration:"));
            Assert.That(yaml, Does.Contain("Prefix: value"));
            Assert.That(yaml, Does.Not.Contain("ProcessorSpecificConfiguration:"));
            Assert.That(yaml, Does.Not.Contain("GeneratorSpecificConfiguration:"));
        });
    }

    [Test]
    public void Bind_WithProcessorConfigurationKey_LoadsConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Name"] = "StubA",
                ["Processor"] = "MyProcessor",
                ["ProcessorConfiguration:Nested:Enabled"] = "true"
            })
            .Build()
            .BindToObject<TransactionStubConfig>(new BinderOptions
            {
                BindNonPublicProperties = true
            });

        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration!.ProcessorConfiguration["Nested:Enabled"], Is.EqualTo("true"));
    }

    [Test]
    public void Bind_WithLegacyProcessorSpecificConfigurationKey_LoadsConfigurationSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Name"] = "StubA",
                ["Processor"] = "MyProcessor",
                ["ProcessorSpecificConfiguration:Nested:Enabled"] = "true"
            })
            .Build()
            .BindToObject<TransactionStubConfig>(new BinderOptions
            {
                BindNonPublicProperties = true
            });

        Assert.That(configuration, Is.Not.Null);
        Assert.That(configuration!.ProcessorConfiguration["Nested:Enabled"], Is.EqualTo("true"));
    }

    [Test]
    public void Build_WithoutName_ThrowsInvalidOperationException()
    {
        var builder = new TransactionStubBuilder()
            .HookNamed("MyProcessor");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void Build_WithoutProcessor_ThrowsInvalidOperationException()
    {
        var builder = new TransactionStubBuilder()
            .Named("StubA");

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Test]
    public void ConfigurationCrud_ReadAndUpdate_WorkAsExpected()
    {
        var builder = new TransactionStubBuilder()
            .Configure(new
            {
                Existing = "value",
                Nested = new
                {
                    Before = "keep"
                }
            });

        builder.UpdateConfiguration(new
        {
            Nested = new
            {
                Added = "new"
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(builder.Configuration["Existing"], Is.EqualTo("value"));
            Assert.That(builder.Configuration["Nested:Before"], Is.EqualTo("keep"));
            Assert.That(builder.Configuration["Nested:Added"], Is.EqualTo("new"));
        });

        builder.Configure(new
        {
            Replaced = "value"
        });
        Assert.That(builder.Configuration["Replaced"], Is.EqualTo("value"));
    }

    [Test]
    public void DataSourceCrud_AddWithReplaceAndClear_WorksAsExpected()
    {
        var builder = new TransactionStubBuilder()
            .AddDataSourceName("source-a")
            .AddDataSourceName("source-b");

        Assert.That(builder.DataSourceNames, Is.EqualTo(new[] { "source-a", "source-b" }));

        builder.UpdateDataSourceName("source-a", "source-c");
        builder.RemoveDataSourceName("source-b");
        Assert.That(builder.DataSourceNames, Is.EqualTo(new[] { "source-c" }));

        builder.AddDataSourceName("source-indexed")
            .RemoveDataSourceNameAt(1);
        Assert.That(builder.DataSourceNames, Is.EqualTo(new[] { "source-c" }));

        builder.RemoveDataSourceName("source-c");
        Assert.That(builder.DataSourceNames, Is.Empty);
    }

    [Test]
    public void DataSourceCrud_AddWithNullCollection_InitializesCollection()
    {
        var builder = new TransactionStubBuilder();
        typeof(TransactionStubBuilder)
            .GetProperty(nameof(TransactionStubBuilder.DataSourceNames))!
            .SetValue(builder, null);

        builder.AddDataSourceName("source-a");

        Assert.That(builder.DataSourceNames, Is.EqualTo(new[] { "source-a" }));
    }

    [Test]
    public void Build_WithSerializationConfigurationAndAliases_PopulatesOptionalFields()
    {
        var requestDeserialization = new DeserializeConfig
        {
            Deserializer = SerializationType.Json
        };
        var responseSerialization = new SerializeConfig
        {
            Serializer = SerializationType.Yaml
        };
        var builder = new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("MyProcessor")
            .Configure(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Feature:Enabled"] = "true" })
                .Build())
            .WithRequestBodyDeserialization(requestDeserialization)
            .WithResponseBodySerialization(responseSerialization);

        var built = builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(builder.ProcessorSpecificConfiguration["Feature:Enabled"], Is.EqualTo("true"));
            Assert.That(built.RequestBodyDeserialization, Is.SameAs(requestDeserialization));
            Assert.That(built.ResponseBodySerialization, Is.SameAs(responseSerialization));
            Assert.That(built.ProcessorConfiguration["Feature:Enabled"], Is.EqualTo("true"));
        });
    }

    [Test]
    public void FromConfig_CreatesEquivalentMutableBuilder()
    {
        var config = new TransactionStubConfig
        {
            Name = "StubA",
            Processor = "MyProcessor",
            DataSourceNames = ["source-a", "source-b"],
            ProcessorConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Retries"] = "2" })
                .Build(),
            RequestBodyDeserialization = new DeserializeConfig
            {
                Deserializer = SerializationType.Json
            },
            ResponseBodySerialization = new SerializeConfig
            {
                Serializer = SerializationType.Xml
            }
        };

        var builder = TransactionStubBuilder.FromConfig(config);
        var rebuilt = builder.Build();

        Assert.Multiple(() =>
        {
            Assert.That(rebuilt.Name, Is.EqualTo(config.Name));
            Assert.That(rebuilt.Processor, Is.EqualTo(config.Processor));
            Assert.That(rebuilt.DataSourceNames, Is.EqualTo(config.DataSourceNames));
            Assert.That(rebuilt.ProcessorConfiguration["Retries"], Is.EqualTo("2"));
            Assert.That(rebuilt.RequestBodyDeserialization, Is.SameAs(config.RequestBodyDeserialization));
            Assert.That(rebuilt.ResponseBodySerialization, Is.SameAs(config.ResponseBodySerialization));
        });
    }

    [Test]
    public void Create_WithObjectAlias_LoadsProcessorConfiguration()
    {
        var builder = new TransactionStubBuilder()
            .Configure(new
            {
                Feature = new
                {
                    Enabled = true
                }
            });

        Assert.That(builder.Configuration["Feature:Enabled"], Is.EqualTo("True"));
    }
}
