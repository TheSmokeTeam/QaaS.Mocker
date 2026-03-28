using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Options;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
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
    public void DataSourceCrud_CreateWithNullBuilder_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.CreateDataSource(null!));
    }

    [Test]
    public void DataSourceCrud_CreateWithoutName_ThrowsArgumentException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.CreateDataSource(new DataSourceBuilder().HookNamed("DummyGenerator")));
    }

    [Test]
    public void DataSourceCrud_UpdateMissingSource_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() =>
            builder.UpdateDataSource("missing", new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator")));
    }

    [Test]
    public void DataSourceCrud_UpdateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.CreateDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"));
        builder.CreateDataSource(new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.UpdateDataSource("SourceA", new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator")));
    }

    [Test]
    public void DataSourceCrud_DeleteMissingSource_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() => builder.DeleteDataSource("missing"));
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
    public void StubCrud_CreateWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.CreateStub((TransactionStubConfig)null!));
    }

    [Test]
    public void StubCrud_CreateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.CreateStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.CreateStub(new TransactionStubBuilder().Named("StubA").HookNamed("OtherProcessor")));
    }

    [Test]
    public void StubCrud_UpdateMissingStub_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() =>
            builder.UpdateStub("missing", new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor")));
    }

    [Test]
    public void StubCrud_UpdateWithNullConfigureAction_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();
        builder.CreateStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));

        Assert.Throws<ArgumentNullException>(() => builder.UpdateStub("StubA", (Action<TransactionStubBuilder>)null!));
    }

    [Test]
    public void StubCrud_UpdateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.CreateStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));
        builder.CreateStub(new TransactionStubBuilder().Named("StubB").HookNamed("DummyProcessor"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.UpdateStub("StubA", new TransactionStubBuilder().Named("StubB").HookNamed("DummyProcessor")));
    }

    [Test]
    public void ServerCrud_CreateServerReadReplaceAddAndUpdate_Works()
    {
        var builder = new ExecutionBuilder();
        var first = BuildHttpServer("ActionA");
        var replacement = BuildSocketServer("ActionB");
        var added = BuildHttpServer("ActionC");

        builder.CreateServer(first);
        var created = builder.ReadServer();
        builder.UpdateServer(server =>
        {
            server.Http = null;
            server.Socket = BuildSocketServer("ActionA").Socket;
        });
        builder.ReplaceServer(replacement);
        builder.AddServer(added);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.SameAs(first));
            Assert.That(builder.ReadServer(), Is.Null);
            Assert.That(builder.ReadServers(), Has.Count.EqualTo(2));
            Assert.That(builder.ReadServers().Select(server => server.ResolveType()),
                Is.EqualTo(new[] { ServerType.Socket, ServerType.Http }));
        });
    }

    [Test]
    public void ServerCrud_CreateServerWhenAlreadyConfigured_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder().CreateServer(BuildHttpServer("ActionA"));

        Assert.Throws<InvalidOperationException>(() => builder.CreateServer(BuildSocketServer("ActionB")));
    }

    [Test]
    public void ServerCrud_CreateServerWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.CreateServer(null!));
    }

    [Test]
    public void ServerCrud_ReplaceServerWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.ReplaceServer(null!));
    }

    [Test]
    public void ServerCrud_AddServerWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddServer(null!));
    }

    [Test]
    public void ServerCrud_AddServerToExistingServers_AppendsToServerList()
    {
        var builder = new ExecutionBuilder()
            .ReplaceServers(
                BuildHttpServer("ActionA"),
                BuildSocketServer("ActionB"));

        builder.AddServer(BuildHttpServer("ActionC"));

        Assert.Multiple(() =>
        {
            Assert.That(builder.ReadServer(), Is.Null);
            Assert.That(builder.ReadServers(), Has.Count.EqualTo(3));
            Assert.That(builder.ReadServers().Select(server => server.ResolveType()),
                Is.EqualTo(new[] { ServerType.Http, ServerType.Socket, ServerType.Http }));
        });
    }

    [Test]
    public void ServerCrud_ReplaceServersWithNullArray_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.ReplaceServers(null!));
    }

    [Test]
    public void ServerCrud_UpdateServerWithoutSingleServer_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.UpdateServer(_ => { }));
    }

    [Test]
    public void ControllerCrud_CreateReadReplaceUpdateAndDelete_Works()
    {
        var builder = new ExecutionBuilder();
        var controller = new ControllerConfig { ServerName = "server-a" };

        builder.CreateController(controller);
        var created = builder.ReadController();
        builder.UpdateController(config => config.ServerName = "server-b");
        builder.ReplaceController(new ControllerConfig { ServerName = "server-c" });
        builder.DeleteController();

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.SameAs(controller));
            Assert.That(created!.ServerName, Is.EqualTo("server-b"));
            Assert.That(builder.ReadController(), Is.Null);
        });
    }

    [Test]
    public void ControllerCrud_CreateControllerWhenAlreadyConfigured_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder().CreateController(new ControllerConfig { ServerName = "server-a" });

        Assert.Throws<InvalidOperationException>(() =>
            builder.CreateController(new ControllerConfig { ServerName = "server-b" }));
    }

    [Test]
    public void ControllerCrud_UpdateWithoutController_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.UpdateController(_ => { }));
    }

    [Test]
    public void ControllerCrud_UpdateWithNullAction_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder().CreateController(new ControllerConfig { ServerName = "server-a" });

        Assert.Throws<ArgumentNullException>(() => builder.UpdateController(null!));
    }

    [Test]
    public void Validate_WithNoServerConfiguration_ReturnsSingleValidationError()
    {
        var builder = new ExecutionBuilder();

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].ErrorMessage, Is.EqualTo("Either 'Server' or 'Servers' must be configured."));
        });
    }

    [Test]
    public void Validate_WithSingleAndMultipleServersConfigured_ReturnsSingleValidationError()
    {
        var builder = new ExecutionBuilder
        {
            Server = BuildHttpServer("ActionA"),
            Servers = [BuildSocketServer("ActionB")]
        };

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results[0].ErrorMessage, Is.EqualTo("Configure either 'Server' or 'Servers', not both."));
        });
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
            .WithExecutionMode(ExecutionMode.Template)
            .CreateStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .ReplaceServer(new ServerConfig
            {
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

    [Test]
    public void Build_WithMultipleServers_ResolvesAndBuilds()
    {
        var context = new InternalContext
        {
            Logger = Globals.Logger,
            RootConfiguration = new ConfigurationBuilder().Build()
        };

        var builder = new ExecutionBuilder()
            .WithContext(context)
            .WithExecutionMode(ExecutionMode.Template)
            .CreateStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .ReplaceServers(
                BuildHttpServer("HealthAction"),
                BuildSocketServer("CollectAction"));

        Assert.DoesNotThrow(() => builder.Build());
    }

    [Test]
    public void Build_WithDuplicateActionNamesAcrossServers_ThrowsInvalidConfigurationsException()
    {
        var context = new InternalContext
        {
            Logger = Globals.Logger,
            RootConfiguration = new ConfigurationBuilder().Build()
        };

        var builder = new ExecutionBuilder()
            .WithContext(context)
            .WithExecutionMode(ExecutionMode.Template)
            .CreateStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .ReplaceServers(
                BuildHttpServer("SharedAction"),
                BuildSocketServer("SharedAction"));

        var exception = Assert.Throws<QaaS.Framework.Configurations.CustomExceptions.InvalidConfigurationsException>(
            () => builder.Build());

        Assert.That(exception!.Message, Is.EqualTo("Configurations are not valid"));
    }

    private static ServerConfig BuildHttpServer(string actionName)
    {
        return new ServerConfig
        {
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
                                Name = actionName,
                                Method = HttpMethod.Get,
                                TransactionStubName = "StubA"
                            }
                        ]
                    }
                ]
            }
        };
    }

    private static ServerConfig BuildSocketServer(string actionName)
    {
        return new ServerConfig
        {
            Socket = new SocketServerConfig
            {
                BindingIpAddress = "127.0.0.1",
                Endpoints =
                [
                    new SocketEndpointConfig
                    {
                        Port = 19090,
                        ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                        SocketType = System.Net.Sockets.SocketType.Stream,
                        TimeoutMs = 1000,
                        BufferSizeBytes = 2048,
                        Action = new SocketActionConfig
                        {
                            Name = actionName,
                            Method = SocketMethod.Collect,
                            TransactionStubName = "StubA"
                        }
                    }
                ]
            }
        };
    }

    public sealed class CodeFirstProcessor : BaseTransactionProcessor<CodeFirstProcessorConfig>
    {
        public override Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData)
            => new() { Body = "ok"u8.ToArray() };
    }

    public sealed class CodeFirstProcessorConfig;
}

