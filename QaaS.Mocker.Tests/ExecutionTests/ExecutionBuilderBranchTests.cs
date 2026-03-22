using System.Reflection;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.SDK.Session.DataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects;
using QaaS.Framework.SDK.Session.SessionDataObjects.RunningSessionsObjects;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Servers.ConfigurationObjects.GrpcServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs;
using QaaS.Mocker.Servers.ConfigurationObjects.SocketServerConfigs;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using HttpMethod = QaaS.Mocker.Servers.ConfigurationObjects.HttpServerConfigs.HttpMethod;

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
    public void DataSourceCrud_CreateWithNullDataSourceCollection_InitializesCollection()
    {
        var builder = new ExecutionBuilder
        {
            DataSources = null!
        };

        builder.CreateDataSource(new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator"));

        Assert.That(builder.ReadDataSource("SourceA"), Is.Not.Null);
    }

    [Test]
    public void DataSourceCrud_UpdateWithNullDataSourceCollection_ThrowsKeyNotFoundException()
    {
        var builder = new ExecutionBuilder
        {
            DataSources = null!
        };

        Assert.Throws<KeyNotFoundException>(() =>
            builder.UpdateDataSource("source-a", new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator")));
    }

    [Test]
    public void StubCrud_UpdateWithConfigureAction_UpdatesExistingStub()
    {
        var builder = new ExecutionBuilder();
        builder.CreateStub(new TransactionStubBuilder().Named("StubA").HookNamed("DummyProcessor"));

        builder.UpdateStub("StubA", update => update.Named("StubB"));

        Assert.Multiple(() =>
        {
            Assert.That(builder.ReadStub("StubA"), Is.Null);
            Assert.That(builder.ReadStub("StubB"), Is.Not.Null);
        });
    }

    [Test]
    public void GetDataSourceGeneratorConfiguration_WithConfiguredGenerator_ReturnsGeneratorConfiguration()
    {
        var generatorConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Prefix"] = "value" })
            .Build();
        var dataSourceBuilder = new DataSourceBuilder()
            .Named("SourceA")
            .HookNamed("DummyGenerator");
        typeof(DataSourceBuilder)
            .GetProperty("GeneratorConfiguration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(dataSourceBuilder, generatorConfiguration);

        var configuration = (IConfiguration)typeof(ExecutionBuilder)
            .GetMethod("GetDataSourceGeneratorConfiguration", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [dataSourceBuilder])!;

        Assert.That(configuration["Prefix"], Is.EqualTo(generatorConfiguration["Prefix"]));
    }

    [Test]
    public void ResolveConfiguredServers_WithSingleServer_ReturnsSingleServerList()
    {
        var builder = new ExecutionBuilder
        {
            Server = new Servers.ConfigurationObjects.ServerConfig
            {
                Http = new Servers.ConfigurationObjects.HttpServerConfigs.HttpServerConfig()
            }
        };

        var configuredServers = (IReadOnlyList<Servers.ConfigurationObjects.ServerConfig>)typeof(ExecutionBuilder)
            .GetMethod("ResolveConfiguredServers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(builder, null)!;

        Assert.That(configuredServers, Has.Count.EqualTo(1));
    }

    [Test]
    public void ResolveConfiguredServers_WithoutConfiguredServers_ReturnsEmptyList()
    {
        var builder = new ExecutionBuilder();

        var configuredServers = (IReadOnlyList<Servers.ConfigurationObjects.ServerConfig>)typeof(ExecutionBuilder)
            .GetMethod("ResolveConfiguredServers", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(builder, null)!;

        Assert.That(configuredServers, Is.Empty);
    }

    [Test]
    public void BuildDataSources_WithNullCollection_ReturnsEmptySequence()
    {
        var builder = new TestExecutionBuilder
        {
            DataSources = null!
        };

        var dataSources = builder.InvokeBuildDataSources();

        Assert.That(dataSources, Is.Empty);
    }

    [Test]
    public void BuildDataSources_WithMissingContextMetadata_InitializesDefaultMetadata()
    {
        var builder = new TestExecutionBuilder
        {
            DataSources =
            [
                new DataSourceBuilder().Named("SourceA").HookNamed("DummyGenerator")
            ]
        };
        builder.WithContext(new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build(),
            InternalRunningSessions = new RunningSessions(new Dictionary<string, RunningSessionData<object, object>>())
        });
        var generators = builder.LoadRuntimeGenerators();
        Assert.That(generators.Select(generator => generator.Key).ToArray(), Is.EqualTo(["SourceA"]));
        Assert.That(generators.Single().Value, Is.Not.Null);

        var dataSources = builder.InvokeBuildDataSources().ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(dataSources, Has.Length.EqualTo(1));
            Assert.That(dataSources[0].Name, Is.EqualTo("SourceA"));
            Assert.That(builder.GetInternalContext().GetMetaDataFromContext(), Is.Not.Null);
        });
    }

    [Test]
    public void Validate_WithSocketEndpointWithoutAction_IgnoresUnnamedSocketEndpoint()
    {
        var builder = new ExecutionBuilder
        {
            Servers =
            [
                new Servers.ConfigurationObjects.ServerConfig
                {
                    Socket = new Servers.ConfigurationObjects.SocketServerConfigs.SocketServerConfig
                    {
                        Endpoints =
                        [
                            new Servers.ConfigurationObjects.SocketServerConfigs.SocketEndpointConfig
                            {
                                Port = 19090,
                                ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                                SocketType = System.Net.Sockets.SocketType.Stream,
                                TimeoutMs = 1000,
                                BufferSizeBytes = 2048,
                                Action = null
                            }
                        ]
                    }
                }
            ]
        };

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void LoadRuntimeGenerators_WithNullDataSources_ReturnsEmptyList()
    {
        var builder = new TestExecutionBuilder
        {
            DataSources = null!
        };

        var generators = builder.LoadRuntimeGenerators();

        Assert.That(generators, Is.Empty);
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

    [Test]
    public void ResolveProcessorTypeName_WithQualifiedName_ReturnsConfiguredValue()
    {
        var fullName = typeof(ExecutionBuilderCrudTests.CodeFirstProcessor).FullName!;

        var resolved = InvokeResolveProcessorTypeName(fullName);

        Assert.That(resolved, Is.EqualTo(fullName));
    }

    [Test]
    public void ResolveProcessorTypeName_WithWhitespace_ReturnsConfiguredValue()
    {
        var resolved = InvokeResolveProcessorTypeName(" ");

        Assert.That(resolved, Is.EqualTo(" "));
    }

    [Test]
    public void ResolveProcessorTypeName_WithUniqueShortName_ReturnsFullName()
    {
        var resolved = InvokeResolveProcessorTypeName(nameof(ExecutionBuilderCrudTests.CodeFirstProcessor));

        Assert.That(resolved, Is.EqualTo(typeof(ExecutionBuilderCrudTests.CodeFirstProcessor).FullName));
    }

    [Test]
    public void GetLoadableTypes_WhenAssemblyThrowsReflectionTypeLoadException_ReturnsAvailableTypes()
    {
        var assembly = new Mock<Assembly>();
        assembly.Setup(instance => instance.GetTypes()).Throws(
            new ReflectionTypeLoadException(
                [typeof(DummyGenerator), null!],
                [new TypeLoadException("boom"), new TypeLoadException("ignored")]));

        var types = InvokeGetLoadableTypes(assembly.Object).ToArray();

        Assert.That(types, Is.EqualTo(new[] { typeof(DummyGenerator) }));
    }

    [Test]
    public void ResolveActionNames_WithHttpGrpcAndSocketServers_UsesConfiguredAndFallbackNames()
    {
        var actionNames = InvokeResolveActionNames(
        [
            new ServerConfig
            {
                Http = new HttpServerConfig
                {
                    Endpoints =
                    [
                        new HttpEndpointConfig
                        {
                            Path = "/health",
                            Actions =
                            [
                                new HttpEndpointActionConfig
                                {
                                    Name = "HttpAction",
                                    Method = HttpMethod.Get,
                                    TransactionStubName = "StubA"
                                }
                            ]
                        }
                    ]
                }
            },
            new ServerConfig
            {
                Grpc = new GrpcServerConfig
                {
                    Services =
                    [
                        new GrpcServiceConfig
                        {
                            ServiceName = "EchoGrpcService",
                            Actions =
                            [
                                new GrpcEndpointActionConfig
                                {
                                    Name = null,
                                    RpcName = "Echo",
                                    TransactionStubName = "StubA"
                                }
                            ]
                        }
                    ]
                }
            },
            new ServerConfig
            {
                Socket = new SocketServerConfig
                {
                    Endpoints =
                    [
                        new SocketEndpointConfig
                        {
                            Port = 19090,
                            ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                            SocketType = System.Net.Sockets.SocketType.Stream,
                            Action = new SocketActionConfig
                            {
                                Name = "SocketAction",
                                Method = SocketMethod.Collect
                            }
                        }
                    ]
                }
            }
        ]).ToArray();

        Assert.That(actionNames, Is.EqualTo(new[] { "HttpAction", "EchoGrpcService.Echo", "SocketAction" }));
    }

    [Test]
    public void ResolveServerTypesSummary_WithMultipleServers_ReturnsCommaSeparatedTypes()
    {
        var summary = InvokeResolveServerTypesSummary(
        [
            BuildHttpServer("HttpAction"),
            BuildSocketServer("SocketAction")
        ]);

        Assert.That(summary, Is.EqualTo("Http, Socket"));
    }

    [Test]
    public void Validate_WithDuplicateActionNamesAcrossServers_ReturnsSortedDuplicates()
    {
        var builder = new ExecutionBuilder
        {
            Servers =
            [
                BuildSocketServer("Zulu"),
                BuildHttpServer("alpha"),
                BuildSocketServer("alpha"),
                BuildHttpServer("Zulu")
            ]
        };

        var results = builder.Validate(new ValidationContext(builder)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Length.EqualTo(1));
            Assert.That(results.Single().ErrorMessage, Does.StartWith("Action names must be unique across 'Servers'. Duplicates: "));
            Assert.That(results.Single().ErrorMessage, Does.Contain("alpha"));
            Assert.That(results.Single().ErrorMessage, Does.Contain("Zulu"));
        });
    }

    [Test]
    public void EnsureDefaultMetaData_WhenMetadataAlreadyExists_LeavesMetadataAccessible()
    {
        var builder = new TestExecutionBuilder();
        var context = builder.GetInternalContext();
        InvokeEnsureDefaultMetaData(builder);

        InvokeEnsureDefaultMetaData(builder);

        Assert.That(context.GetMetaDataFromContext(), Is.Not.Null);
    }

    [Test]
    public void EnsureDefaultMetaData_WhenStoredMetadataIsInvalid_ReplacesValue()
    {
        var builder = new TestExecutionBuilder();
        var context = builder.GetInternalContext();
        context.InsertValueIntoGlobalDictionary(context.GetMetaDataPath(), "invalid");

        Assert.DoesNotThrow(() => InvokeEnsureDefaultMetaData(builder));
        Assert.That(context.GetMetaDataFromContext(), Is.Not.Null);
    }

    private static string InvokeResolveProcessorTypeName(string configuredProcessor)
    {
        return (string)typeof(ExecutionBuilder)
            .GetMethod("ResolveProcessorTypeName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [configuredProcessor])!;
    }

    private static IEnumerable<Type> InvokeGetLoadableTypes(Assembly assembly)
    {
        return (IEnumerable<Type>)typeof(ExecutionBuilder)
            .GetMethod("GetLoadableTypes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [assembly])!;
    }

    private static IEnumerable<string> InvokeResolveActionNames(IEnumerable<ServerConfig> servers)
    {
        return (IEnumerable<string>)typeof(ExecutionBuilder)
            .GetMethod("ResolveActionNames", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [servers])!;
    }

    private static string InvokeResolveServerTypesSummary(IEnumerable<ServerConfig> servers)
    {
        return (string)typeof(ExecutionBuilder)
            .GetMethod("ResolveServerTypesSummary", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [servers])!;
    }

    private static void InvokeEnsureDefaultMetaData(ExecutionBuilder builder)
    {
        typeof(ExecutionBuilder)
            .GetMethod("EnsureDefaultMetaData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(builder, null);
    }

    private static ServerConfig BuildHttpServer(string actionName)
    {
        return new ServerConfig
        {
            Http = new HttpServerConfig
            {
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
                Endpoints =
                [
                    new SocketEndpointConfig
                    {
                        Port = 19090,
                        ProtocolType = System.Net.Sockets.ProtocolType.Tcp,
                        SocketType = System.Net.Sockets.SocketType.Stream,
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

    private sealed class TestExecutionBuilder : ExecutionBuilder
    {
        public IEnumerable<DataSource> InvokeBuildDataSources() => BuildDataSources();

        public IList<KeyValuePair<string, IGenerator>> LoadRuntimeGenerators()
        {
            typeof(ExecutionBuilder)
                .GetMethod("LoadContextScopeDependencies", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(this, null);
            return ((ILifetimeScope)typeof(ExecutionBuilder)
                    .GetField("_scope", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(this)!)
                .Resolve<IList<KeyValuePair<string, IGenerator>>>();
        }

        public InternalContext GetInternalContext() => Context;
    }

    private sealed class DummyGenerator : IGenerator
    {
        public Context Context { get; set; } = null!;

        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];

        public IEnumerable<Data<object>> Generate(IImmutableList<SessionData> sessionDataList,
            IImmutableList<DataSource> dataSourceList)
        {
            return [];
        }
    }

    private sealed class DummyProcessor : ITransactionProcessor
    {
        public Context Context { get; set; } = null!;

        public List<ValidationResult>? LoadAndValidateConfiguration(IConfiguration configuration) => [];

        public Data<object> Process(IImmutableList<DataSource> dataSourceList, Data<object> requestData) => requestData;
    }
}
