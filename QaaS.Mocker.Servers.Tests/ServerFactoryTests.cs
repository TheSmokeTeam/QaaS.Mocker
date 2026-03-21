using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Servers.Servers;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker.Servers.Tests;

[TestFixture]
public class ServerFactoryTests
{
    [Test]
    public void Build_WithHttpServerType_ReturnsHttpServer()
    {
        var factory = new ServerFactory(Globals.Context, new ServerConfig
        {
            Http = new HttpServerConfig { Port = 8080, Endpoints = [] }
        });

        var server = factory.Build(ImmutableList<DataSource>.Empty, CreateRequiredStubs());

        Assert.That(server, Is.TypeOf<HttpServer>());
    }

    [Test]
    public void Build_WithGrpcServerType_ReturnsGrpcServer()
    {
        var factory = new ServerFactory(Globals.Context, new ServerConfig
        {
            Grpc = new GrpcServerConfig { Port = 50051, Services = [] }
        });

        var server = factory.Build(ImmutableList<DataSource>.Empty, CreateRequiredStubs());

        Assert.That(server, Is.TypeOf<GrpcServer>());
    }

    [Test]
    public void Build_WithSocketServerType_ReturnsSocketServer()
    {
        var factory = new ServerFactory(Globals.Context, new ServerConfig
        {
            Socket = new SocketServerConfig { Endpoints = [] }
        });

        var server = factory.Build(ImmutableList<DataSource>.Empty, ImmutableList<TransactionStub>.Empty);

        Assert.That(server, Is.TypeOf<SocketServer>());
    }

    [Test]
    public void Build_WithMultipleServerConfigurations_ReturnsCompositeServer()
    {
        var factory = new ServerFactory(Globals.Context,
        [
            new ServerConfig
            {
                Http = new HttpServerConfig { Port = 8080, Endpoints = [] }
            },
            new ServerConfig
            {
                Grpc = new GrpcServerConfig { Port = 50051, Services = [] }
            }
        ]);

        var server = factory.Build(ImmutableList<DataSource>.Empty, CreateRequiredStubs());

        Assert.That(server, Is.TypeOf<CompositeServer>());
    }

    [Test]
    public void Build_WithNoTransportConfigured_ThrowsInvalidOperationException()
    {
        var factory = new ServerFactory(Globals.Context, new ServerConfig());

        Assert.Throws<InvalidOperationException>(() =>
            factory.Build(ImmutableList<DataSource>.Empty, ImmutableList<TransactionStub>.Empty));
    }

    [Test]
    public void Build_WithNoServerConfigurations_ThrowsArgumentException()
    {
        var factory = new ServerFactory(Globals.Context, Array.Empty<ServerConfig>());

        Assert.Throws<ArgumentException>(() =>
            factory.Build(ImmutableList<DataSource>.Empty, ImmutableList<TransactionStub>.Empty));
    }

    private static IImmutableList<TransactionStub> CreateRequiredStubs()
    {
        return
        [
            new TransactionStub
            {
                Name = Constants.DefaultNotFoundTransactionStubLabel,
                Processor = new NoOpProcessor(),
                DataSourceList = ImmutableList<DataSource>.Empty
            },
            new TransactionStub
            {
                Name = Constants.DefaultInternalErrorTransactionStubLabel,
                Processor = new NoOpProcessor(),
                DataSourceList = ImmutableList<DataSource>.Empty
            }
        ];
    }

    private sealed class NoOpProcessor : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;
        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];
        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => requestData;
    }
}
