using System.Collections.Immutable;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

namespace QaaS.Mocker.Tests.ExecutionTests;

[TestFixture]
public class ExecutionBuilderCrudTests
{
    [Test]
    public void DataSourceCrud_CreateReadUpdateDelete_Works()
    {
        var builder = new ExecutionBuilder();
        var first = new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator");
        var replacement = new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator");

        builder.CreateDataSource(first);
        var readCreated = builder.ReadDataSource("sourcea");
        builder.UpdateDataSource("sourcea", replacement);
        var readUpdated = builder.ReadDataSource("sourceb");
        builder.DeleteDataSource("sourceb");

        Assert.Multiple(() =>
        {
            Assert.That(readCreated, Is.Not.Null);
            Assert.That(readUpdated, Is.Not.Null);
            Assert.That(builder.ReadDataSource("sourcea"), Is.Null);
            Assert.That(builder.ReadDataSource("sourceb"), Is.Null);
        });
    }

    [Test]
    public void DataSourceCrud_CreateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.CreateDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.CreateDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("OtherGenerator")));
    }

    [Test]
    public void StubCrud_CreateReadUpdateDelete_Works()
    {
        var builder = new ExecutionBuilder();

        builder.CreateStub(new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("DummyProcessor"));
        var readCreated = builder.ReadStub("stuba");
        builder.UpdateStub("stuba", update => update
            .Named("StubB")
            .HookNamed("DummyProcessor"));
        var readUpdated = builder.ReadStub("stubb");
        builder.DeleteStub("stubb");

        Assert.Multiple(() =>
        {
            Assert.That(readCreated, Is.Not.Null);
            Assert.That(readUpdated, Is.Not.Null);
            Assert.That(builder.ReadStub("stuba"), Is.Null);
            Assert.That(builder.ReadStub("stubb"), Is.Null);
        });
    }

    [Test]
    public void StubCrud_DeleteMissingStub_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() => builder.DeleteStub("missing"));
    }

    [Test]
    public void Build_WithCodeFirstConfiguration_ResolvesProcessorHookAndBuilds()
    {
        var context = new InternalContext
        {
            Logger = Globals.Logger,
            RootConfiguration = new ConfigurationBuilder().Build()
        };

        var builder = new ExecutionBuilder()
            .WithContext(context)
            .WithExecutionMode(ExecutionMode.Lint)
            .CreateStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .ReplaceServer(new ServerConfig
            {
                Type = ServerType.Http,
                Http = new HttpServerConfig
                {
                    Port = 18081,
                    IsLocalhost = true,
                    Endpoints =
                    [
                        new HttpEndpointConfig
                        {
                            Path = "/health",
                            Actions =
                            [
                                new HttpEndpointActionConfig
                                {
                                    Name = "Health",
                                    Method = HttpMethod.Get,
                                    TransactionStubName = "StubA"
                                }
                            ]
                        }
                    ]
                }
            });

        Assert.DoesNotThrow(() => builder.Build());
    }

    public sealed class CodeFirstProcessor : BaseTransactionProcessor<CodeFirstProcessorConfig>
    {
        public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
            => new() { Body = "ok"u8.ToArray() };
    }

    public sealed class CodeFirstProcessorConfig;
}
