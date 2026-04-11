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

        builder.AddDataSource(first);
        var readCreated = builder.DataSources!.FirstOrDefault(source =>
            string.Equals(source.Name, "sourcea", StringComparison.OrdinalIgnoreCase));
        builder.UpdateDataSource("sourcea", replacement);
        var readUpdated = builder.DataSources!.FirstOrDefault(source =>
            string.Equals(source.Name, "sourceb", StringComparison.OrdinalIgnoreCase));
        builder.RemoveDataSource("sourceb");

        Assert.Multiple(() =>
        {
            Assert.That(readCreated, Is.Not.Null);
            Assert.That(readUpdated, Is.Not.Null);
            Assert.That(builder.DataSources!.FirstOrDefault(source =>
                string.Equals(source.Name, "sourcea", StringComparison.OrdinalIgnoreCase)), Is.Null);
            Assert.That(builder.DataSources!.FirstOrDefault(source =>
                string.Equals(source.Name, "sourceb", StringComparison.OrdinalIgnoreCase)), Is.Null);
        });
    }

    [Test]
    public void DataSourceCrud_CreateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.AddDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("OtherGenerator")));
    }

    [Test]
    public void DataSourceCrud_CreateWithNullBuilder_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddDataSource(null!));
    }

    [Test]
    public void DataSourceCrud_CreateWithoutName_ThrowsArgumentException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentException>(() =>
            builder.AddDataSource(new DataSourceBuilder().HookNamed("DummyGenerator")));
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
        builder.AddDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"));
        builder.AddDataSource(new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.UpdateDataSource("SourceA", new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator")));
    }

    [Test]
    public void DataSourceCrud_RemoveAt_RemovesSourceByIndex()
    {
        var builder = new ExecutionBuilder()
            .AddDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"))
            .AddDataSource(new DataSourceBuilder().Named("SourceB").HookNamed("DummyGenerator"));

        builder.RemoveDataSourceAt(0);

        Assert.Multiple(() =>
        {
            Assert.That(builder.DataSources!.Single().Name, Is.EqualTo("SourceB"));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.RemoveDataSourceAt(1));
        });
    }

    [Test]
    public void DataSourceCrud_DeleteMissingSource_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() => builder.RemoveDataSource("missing"));
    }

    [Test]
    public void StubCrud_CreateReadUpdateDelete_Works()
    {
        var builder = new ExecutionBuilder();

        builder.AddStub(new TransactionStubBuilder()
            .Named("StubA")
            .HookNamed("DummyProcessor"));
        var readCreated = builder.Stubs.FirstOrDefault(stub =>
            string.Equals(stub.Name, "stuba", StringComparison.OrdinalIgnoreCase));
        builder.UpdateStub("stuba", new TransactionStubBuilder()
            .Named("StubB")
            .HookNamed("DummyProcessor"));
        var readUpdated = builder.Stubs.FirstOrDefault(stub =>
            string.Equals(stub.Name, "stubb", StringComparison.OrdinalIgnoreCase));
        builder.RemoveStub("stubb");

        Assert.Multiple(() =>
        {
            Assert.That(readCreated, Is.Not.Null);
            Assert.That(readUpdated, Is.Not.Null);
            Assert.That(builder.Stubs.FirstOrDefault(stub =>
                string.Equals(stub.Name, "stuba", StringComparison.OrdinalIgnoreCase)), Is.Null);
            Assert.That(builder.Stubs.FirstOrDefault(stub =>
                string.Equals(stub.Name, "stubb", StringComparison.OrdinalIgnoreCase)), Is.Null);
        });
    }

    [Test]
    public void StubCrud_DeleteMissingStub_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() => builder.RemoveStub("missing"));
    }

    [Test]
    public void StubCrud_CreateWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddStub((TransactionStubConfig)null!));
    }

    [Test]
    public void StubCrud_CreateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.AddStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddStub(new TransactionStubBuilder().Named("StubA").HookNamed("OtherProcessor")));
    }

    [Test]
    public void StubCrud_UpdateMissingStub_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<KeyNotFoundException>(() =>
            builder.UpdateStub("missing", new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor")));
    }

    [Test]
    public void StubCrud_UpdateWithNullBuilder_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();
        builder.AddStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));

        Assert.Throws<ArgumentNullException>(() => builder.UpdateStub("StubA", (TransactionStubBuilder)null!));
    }

    [Test]
    public void StubCrud_UpdateWithDuplicateName_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();
        builder.AddStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));
        builder.AddStub(new TransactionStubBuilder().Named("StubB").HookNamed("DummyProcessor"));

        Assert.Throws<InvalidOperationException>(() =>
            builder.UpdateStub("StubA", new TransactionStubBuilder().Named("StubB").HookNamed("DummyProcessor")));
    }

    [Test]
    public void StubCrud_RemoveAt_RemovesStubByIndex()
    {
        var builder = new ExecutionBuilder()
            .AddStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"))
            .AddStub(new TransactionStubBuilder().Named("StubB").HookNamed("DummyProcessor"));

        builder.RemoveStubAt(0);

        Assert.Multiple(() =>
        {
            Assert.That(builder.Stubs.Single().Name, Is.EqualTo("StubB"));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.RemoveStubAt(1));
        });
    }

    [Test]
    public void ServerCrud_CreateServerReadUpdateAndIndexedServerCrud_Works()
    {
        var builder = new ExecutionBuilder();
        var first = BuildHttpServer("ActionA");
        var replacement = BuildSocketServer("ActionB");
        var added = BuildHttpServer("ActionC");

        builder.WithServer(first);
        var created = builder.Server;
        builder.UpdateServer(BuildSocketServer("ActionA"));
        builder.UpdateServer(replacement);
        var updatedSingle = builder.Server;
        builder.RemoveServer();
        builder.AddServers(replacement, added);
        var createdIndexed = builder.Servers[1];
        builder.UpdateServerAt(1, BuildSocketServer("ActionC"));
        builder.RemoveServerAt(1);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.SameAs(first));
            Assert.That(updatedSingle, Is.SameAs(replacement));
            Assert.That(builder.Server, Is.Null);
            Assert.That(createdIndexed, Is.SameAs(added));
            Assert.That(builder.Servers, Has.Length.EqualTo(1));
            Assert.That(builder.Servers.ElementAtOrDefault(0)?.ResolveType(), Is.EqualTo(ServerType.Socket));
            Assert.That(builder.Servers.ElementAtOrDefault(1), Is.Null);
        });
    }

    [Test]
    public void ServerCrud_WithServerWhenAlreadyConfigured_ReplacesConfiguredServer()
    {
        var builder = new ExecutionBuilder().WithServer(BuildHttpServer("ActionA"));

        builder.WithServer(BuildSocketServer("ActionB"));

        Assert.That(builder.Server!.ResolveType(), Is.EqualTo(ServerType.Socket));
    }

    [Test]
    public void ServerCrud_CreateServerWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.WithServer(null!));
    }

    [Test]
    public void ServerCrud_UpdateServerWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder().WithServer(BuildHttpServer("ActionA"));

        Assert.Throws<ArgumentNullException>(() => builder.UpdateServer((ServerConfig)null!));
    }

    [Test]
    public void ServerCrud_CreateServersWithNullConfigArray_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.AddServers(null!));
    }

    [Test]
    public void ServerCrud_CreateServers_StoresMultipleServerList()
    {
        var builder = new ExecutionBuilder()
            .AddServers(
                BuildHttpServer("ActionA"),
                BuildSocketServer("ActionB"));

        Assert.Multiple(() =>
        {
            Assert.That(builder.Server, Is.Null);
            Assert.That(builder.Servers, Has.Length.EqualTo(2));
            Assert.That(builder.Servers.Select(server => server.ResolveType()),
                Is.EqualTo(new[] { ServerType.Http, ServerType.Socket }));
        });
    }

    [Test]
    public void ServerCrud_UpdateServerAtWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder().AddServers(BuildHttpServer("ActionA"));

        Assert.Throws<ArgumentNullException>(() => builder.UpdateServerAt(0, null!));
    }

    [Test]
    public void ServerCrud_UpdateServerWithoutSingleServer_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.UpdateServer(BuildHttpServer("ActionA")));
    }

    [Test]
    public void ControllerCrud_CreateReadReplaceUpdateAndDelete_Works()
    {
        var builder = new ExecutionBuilder();
        var controller = new ControllerConfig { ServerName = "server-a" };

        builder.WithController(controller);
        var created = builder.Controller;
        builder.UpdateController(new ControllerConfig { ServerName = "server-b" });
        builder.UpdateController(new ControllerConfig { ServerName = "server-c" });
        var updated = builder.Controller;
        builder.RemoveController();

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.SameAs(controller));
            Assert.That(created!.ServerName, Is.EqualTo("server-a"));
            Assert.That(updated!.ServerName, Is.EqualTo("server-c"));
            Assert.That(builder.Controller, Is.Null);
        });
    }

    [Test]
    public void ControllerCrud_WithControllerWhenAlreadyConfigured_ReplacesConfiguredController()
    {
        var builder = new ExecutionBuilder().WithController(new ControllerConfig { ServerName = "server-a" });

        builder.WithController(new ControllerConfig { ServerName = "server-b" });

        Assert.That(builder.Controller!.ServerName, Is.EqualTo("server-b"));
    }

    [Test]
    public void ControllerCrud_UpdateWithoutController_ThrowsInvalidOperationException()
    {
        var builder = new ExecutionBuilder();

        Assert.Throws<InvalidOperationException>(() => builder.UpdateController(new ControllerConfig()));
    }

    [Test]
    public void ControllerCrud_UpdateWithNullConfig_ThrowsArgumentNullException()
    {
        var builder = new ExecutionBuilder().WithController(new ControllerConfig { ServerName = "server-a" });

        Assert.Throws<ArgumentNullException>(() => builder.UpdateController((ControllerConfig)null!));
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
            .AddStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .WithServer(new ServerConfig
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
            .AddStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .AddServers(
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
            .AddStub(new TransactionStubBuilder()
                .Named("StubA")
                .HookNamed(nameof(CodeFirstProcessor)))
            .AddServers(
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

