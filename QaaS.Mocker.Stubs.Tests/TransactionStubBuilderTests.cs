using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.Configurations;
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
            .CreateConfiguration(config)
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
            .CreateConfiguration(configuration)
            .Build();

        Assert.That(built.ProcessorConfiguration["Nested:Enabled"], Is.EqualTo("true"));
    }

    [Test]
    public void Build_SerializesProcessorConfigurationWithUpdatedKey()
    {
        var built = new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("MyProcessor")
            .CreateConfiguration(new { Prefix = "value" })
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
    public void ConfigurationCrud_ReadUpdateAndDelete_WorkAsExpected()
    {
        var builder = new TransactionStubBuilder()
            .CreateConfiguration(new
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
            Assert.That(builder.ReadConfiguration()["Existing"], Is.EqualTo("value"));
            Assert.That(builder.ReadConfiguration()["Nested:Before"], Is.EqualTo("keep"));
            Assert.That(builder.ReadConfiguration()["Nested:Added"], Is.EqualTo("new"));
        });

        builder.DeleteConfiguration();
        Assert.That(builder.ReadConfiguration().AsEnumerable().Any(), Is.False);
    }
}
