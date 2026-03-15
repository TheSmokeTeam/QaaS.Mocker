using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Stubs.ConfigurationObjects;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ExecutionBuilderBranchTests
{
    [Test]
    public void DataSourceOperations_WithNullCollectionFallback_ReturnSafeResults()
    {
        var builder = new ExecutionBuilder
        {
            DataSources = null!
        };

        Assert.Multiple(() =>
        {
            Assert.That(builder.ReadDataSource("missing"), Is.Null);
            Assert.Throws<KeyNotFoundException>(() => builder.DeleteDataSource("missing"));
        });
    }

    [Test]
    public void DataSourceCrud_UpdateWithUnnamedBuilder_ThrowsArgumentException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.UpdateDataSource("source-a", new DataSourceBuilder().HookNamed("DummyGenerator")));
    }

    [Test]
    public void StubCrud_CreateWithUnnamedConfig_ThrowsArgumentException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.CreateStub(new TransactionStubConfig { Processor = "DummyProcessor" }));
    }

    [Test]
    public void StubCrud_UpdateWithUnnamedConfig_ThrowsArgumentException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.UpdateStub("stub-a", new TransactionStubConfig { Processor = "DummyProcessor" }));
    }

    [Test]
    public void GetDataSourceGeneratorName_WithHookConfigured_ReturnsHookName()
    {
        var dataSourceBuilder = new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator");

        var generatorName = (string)typeof(ExecutionBuilder)
            .GetMethod("GetDataSourceGeneratorName", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [dataSourceBuilder])!;

        Assert.That(generatorName, Is.EqualTo("DummyGenerator"));
    }

    [Test]
    public void GetDataSourceGeneratorName_WithoutHookConfigured_ThrowsInvalidOperationException()
    {
        var dataSourceBuilder = new DataSourceBuilder().Named("SourceA");

        var exception = Assert.Throws<TargetInvocationException>(() =>
            typeof(ExecutionBuilder)
                .GetMethod("GetDataSourceGeneratorName", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [dataSourceBuilder]));

        Assert.That(exception!.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void GetDataSourceGeneratorConfiguration_WithoutGeneratorConfiguration_ReturnsEmptyConfiguration()
    {
        var dataSourceBuilder = new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator");

        var configuration = (IConfiguration)typeof(ExecutionBuilder)
            .GetMethod("GetDataSourceGeneratorConfiguration", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [dataSourceBuilder])!;

        Assert.That(configuration.AsEnumerable().Any(), Is.False);
    }

    [Test]
    public void BuilderContextFluentMethods_CloneAndUpdateExecutionContext()
    {
        var originalConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:Type"] = "Http" })
            .Build();
        var updatedConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Server:Type"] = "Grpc" })
            .Build();
        var originalContext = new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = originalConfiguration,
            ExecutionId = "execution-1",
            CaseName = "case-a"
        };

        var builder = new ExecutionBuilder()
            .WithContext(originalContext)
            .WithLogger(Globals.Logger)
            .WithRootConfiguration(updatedConfiguration)
            .WithExecutionMode(Options.ExecutionMode.Template)
            .RunLocally()
            .WithTemplateOutputFolder("templates");

        var executionMode = (Options.ExecutionMode)typeof(ExecutionBuilder)
            .GetField("_executionMode", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(builder)!;
        var runLocally = (bool)typeof(ExecutionBuilder)
            .GetField("_runLocally", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(builder)!;
        var outputFolder = (string?)typeof(ExecutionBuilder)
            .GetField("_templateOutputFolder", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(builder);
        var context = (InternalContext)typeof(ExecutionBuilder)
            .GetMethod("CloneContext", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(builder, [Globals.Logger, updatedConfiguration])!;

        Assert.Multiple(() =>
        {
            Assert.That(context.Logger, Is.SameAs(Globals.Logger));
            Assert.That(context.RootConfiguration["Server:Type"], Is.EqualTo("Grpc"));
            Assert.That(context.ExecutionId, Is.EqualTo("execution-1"));
            Assert.That(context.CaseName, Is.EqualTo("case-a"));
            Assert.That(executionMode, Is.EqualTo(Options.ExecutionMode.Template));
            Assert.That(runLocally, Is.True);
            Assert.That(outputFolder, Is.EqualTo("templates"));
        });
    }
}
