using System.Collections.Immutable;
using System.Reflection;
using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Stubs.ConfigurationObjects;

namespace QaaS.Mocker.Stubs.Tests;

[TestFixture]
public class StubFactoryTests
{
    [Test]
    public void Build_WithConfiguredStub_AddsMappedAndDefaultStubs()
    {
        var processor = new Mock<ITransactionProcessor>();
        var dataSource = new DataSource { Name = "DataA" };
        var factory = new StubFactory(
            Globals.Context,
            [
                new TransactionStubConfig
                {
                    Name = "StubA",
                    Processor = "ProcessorA",
                    DataSourceNames = ["DataA"]
                }
            ],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", processor.Object)
            });

        var stubs = factory.Build([dataSource]);

        Assert.Multiple(() =>
        {
            Assert.That(stubs.Select(stub => stub.Name), Does.Contain("StubA"));
            Assert.That(stubs.Select(stub => stub.Name), Does.Contain(Constants.DefaultNotFoundTransactionStubLabel));
            Assert.That(stubs.Select(stub => stub.Name), Does.Contain(Constants.DefaultInternalErrorTransactionStubLabel));
            Assert.That(stubs.Single(stub => stub.Name == "StubA").DataSourceList.Single(), Is.SameAs(dataSource));
        });
    }

    [Test]
    public void Build_DefaultFallbackStubs_ReturnConfiguredStatusCodes()
    {
        var processor = new Mock<ITransactionProcessor>();
        var factory = new StubFactory(
            Globals.Context,
            [
                new TransactionStubConfig
                {
                    Name = "StubA",
                    Processor = "ProcessorA"
                }
            ],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", processor.Object)
            });

        var stubs = factory.Build(ImmutableList<DataSource>.Empty);
        var notFoundResponse = stubs.Single(stub => stub.Name == Constants.DefaultNotFoundTransactionStubLabel)
            .Exercise(new Data<object> { Body = Array.Empty<byte>() });
        var internalErrorResponse = stubs.Single(stub => stub.Name == Constants.DefaultInternalErrorTransactionStubLabel)
            .Exercise(new Data<object> { Body = Array.Empty<byte>() });

        Assert.Multiple(() =>
        {
            Assert.That(notFoundResponse.MetaData?.Http?.StatusCode, Is.EqualTo(Constants.DefaultNotFoundTransactionStubStatusCode));
            Assert.That(internalErrorResponse.MetaData?.Http?.StatusCode, Is.EqualTo(Constants.DefaultInternalErrorTransactionStubStatusCode));
            Assert.That(notFoundResponse.Body, Is.EqualTo(Array.Empty<byte>()));
            Assert.That(internalErrorResponse.Body, Is.EqualTo(Array.Empty<byte>()));
        });
    }

    [Test]
    public void Build_WhenProcessorMappingIsMissing_ThrowsArgumentException()
    {
        var factory = new StubFactory(
            Globals.Context,
            [
                new TransactionStubConfig
                {
                    Name = "StubA",
                    Processor = "ProcessorA"
                }
            ],
            []);

        var exception = Assert.Throws<ArgumentException>(() => factory.Build(ImmutableList<DataSource>.Empty));

        Assert.That(exception!.Message, Does.Contain("Transaction Stub StubA"));
    }

    [Test]
    public void Build_WhenConfiguredDataSourceIsMissing_ThrowsArgumentException()
    {
        var factory = new StubFactory(
            Globals.Context,
            [
                new TransactionStubConfig
                {
                    Name = "StubA",
                    Processor = "ProcessorA",
                    DataSourceNames = ["MissingSource"]
                }
            ],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", new Mock<ITransactionProcessor>().Object)
            });

        var exception = Assert.Throws<ArgumentException>(() => factory.Build(ImmutableList<DataSource>.Empty));

        Assert.That(exception!.Message, Does.Contain("Could not find data source MissingSource"));
    }

    [Test]
    public void Build_WithSerializerAndDeserializerConfiguration_InitializesStubPipelines()
    {
        var config = new TransactionStubConfig
        {
            Name = "StubA",
            Processor = "ProcessorA"
        };
        typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.RequestBodyDeserialization))!
            .SetValue(config, CreateNonNullOption(nameof(TransactionStubConfig.RequestBodyDeserialization)));
        typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.ResponseBodySerialization))!
            .SetValue(config, CreateNonNullOption(nameof(TransactionStubConfig.ResponseBodySerialization)));

        var factory = new StubFactory(
            Globals.Context,
            [config],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", new Mock<ITransactionProcessor>().Object)
            });

        var stub = factory.Build(ImmutableList<DataSource>.Empty).Single(instance => instance.Name == "StubA");

        Assert.Multiple(() =>
        {
            Assert.That(stub.Name, Is.EqualTo("StubA"));
            Assert.That(typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.RequestBodyDeserialization))!
                .GetValue(config), Is.Not.Null);
            Assert.That(typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.ResponseBodySerialization))!
                .GetValue(config), Is.Not.Null);
        });
    }

    [Test]
    public void Build_WithOnlyRequestDeserializerConfiguration_InitializesDeserializer()
    {
        var config = new TransactionStubConfig
        {
            Name = "StubA",
            Processor = "ProcessorA"
        };
        typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.RequestBodyDeserialization))!
            .SetValue(config, CreateNonNullOption(nameof(TransactionStubConfig.RequestBodyDeserialization)));

        var factory = new StubFactory(
            Globals.Context,
            [config],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", new Mock<ITransactionProcessor>().Object)
            });

        var stub = factory.Build(ImmutableList<DataSource>.Empty).Single(instance => instance.Name == "StubA");

        Assert.That(stub.Name, Is.EqualTo("StubA"));
    }

    [Test]
    public void Build_WithOnlyResponseSerializerConfiguration_InitializesSerializer()
    {
        var config = new TransactionStubConfig
        {
            Name = "StubA",
            Processor = "ProcessorA"
        };
        typeof(TransactionStubConfig).GetProperty(nameof(TransactionStubConfig.ResponseBodySerialization))!
            .SetValue(config, CreateNonNullOption(nameof(TransactionStubConfig.ResponseBodySerialization)));

        var factory = new StubFactory(
            Globals.Context,
            [config],
            new List<KeyValuePair<string, ITransactionProcessor>>
            {
                new("StubA", new Mock<ITransactionProcessor>().Object)
            });

        var stub = factory.Build(ImmutableList<DataSource>.Empty).Single(instance => instance.Name == "StubA");

        Assert.That(stub.Name, Is.EqualTo("StubA"));
    }

    private static object CreateNonNullOption(string propertyName)
    {
        var property = typeof(TransactionStubConfig).GetProperty(propertyName)!;
        var option = Activator.CreateInstance(property.PropertyType!)!;
        foreach (var candidateProperty in property.PropertyType!.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(instance => instance.CanWrite && instance.PropertyType.IsEnum))
        {
            var values = Enum.GetValues(candidateProperty.PropertyType);
            candidateProperty.SetValue(option, values.GetValue(values.Length > 1 ? 1 : 0));
        }

        return option;
    }
}
