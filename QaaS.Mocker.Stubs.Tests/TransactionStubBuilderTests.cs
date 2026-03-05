using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Mocker.Stubs.ConfigurationObjects;

namespace QaaS.Mocker.Stubs.Tests;

[TestFixture]
public class TransactionStubBuilderTests
{
    [Test]
    public void Build_WithObjectConfiguration_LoadsProcessorSpecificConfiguration()
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
            Assert.That(built.ProcessorSpecificConfiguration["Prefix"], Is.EqualTo("value"));
            Assert.That(built.ProcessorSpecificConfiguration["Retries"], Is.EqualTo("3"));
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

        Assert.That(built.ProcessorSpecificConfiguration["Nested:Enabled"], Is.EqualTo("true"));
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
}
