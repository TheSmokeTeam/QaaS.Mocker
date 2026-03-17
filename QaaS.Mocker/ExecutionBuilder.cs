using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Executions;
using QaaS.Framework.Providers;
using QaaS.Framework.Providers.Modules;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.SDK;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Framework.SDK.Extensions;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Mocker.Controller;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Stubs;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using QaaS.Mocker.Stubs.Stubs;

namespace QaaS.Mocker;

public class ExecutionBuilder : BaseExecutionBuilder<InternalContext, ExecutionData>, IValidatableObject
{
    private static readonly PropertyInfo DataSourceGeneratorProperty =
        typeof(DataSourceBuilder).GetProperty("Generator", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Could not resolve DataSourceBuilder.Generator property.");

    private static readonly PropertyInfo DataSourceGeneratorConfigurationProperty =
        typeof(DataSourceBuilder).GetProperty("GeneratorConfiguration", BindingFlags.Instance | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Could not resolve DataSourceBuilder.GeneratorConfiguration property.");

    [UniquePropertyInEnumerable(nameof(TransactionStubConfig.Name)),
     Description("List of transaction stubs that can be used for server actions." +
                 "They provide processing functionality to exercise transaction data.")]
    public TransactionStubConfig[] Stubs { get; set; } = [];

    [Description("The legacy single server mocker instance to run.")]
    public ServerConfig? Server { get; set; }

    [Description("List of server mocker instances to run concurrently.")]
    public ServerConfig[] Servers { get; set; } = [];

    [Description("The server mocker controller configuration")]
    public ControllerConfig? Controller { get; set; }

    private ILifetimeScope _scope;
    private readonly List<ValidationResult> _validationResults;

    private ExecutionMode _executionMode = ExecutionMode.Run;
    private bool _runLocally;
    private string? _templateOutputFolder;

    protected override IEnumerable<DataSource> BuildDataSources()
    {
        EnsureDefaultMetaData();

        var configuredDataSources = DataSources ?? [];
        var dataSources = configuredDataSources
            .Select(dataSourceBuilder => dataSourceBuilder.Register())
            .ToImmutableList();
        var resolvedGenerators = _scope.Resolve<IList<KeyValuePair<string, IGenerator>>>();

        return configuredDataSources.Select(dataSourceBuilder =>
        {
            var configuredGenerator = GetDataSourceGeneratorName(dataSourceBuilder);

            // Scope generator hook instances by data source name so overlays can introduce
            // additional sources without forcing their Name to match the hook type.
            DataSourceGeneratorProperty.SetValue(dataSourceBuilder, dataSourceBuilder.Name);

            try
            {
                return dataSourceBuilder.Build(Context, dataSources, resolvedGenerators);
            }
            finally
            {
                DataSourceGeneratorProperty.SetValue(dataSourceBuilder, configuredGenerator);
            }
        });
    }

    internal ExecutionBuilder(
        InternalContext context,
        ExecutionMode executionMode,
        bool runLocally,
        string? templateOutputFolder) : this()
    {
        Context = context;
        _executionMode = executionMode;
        _runLocally = runLocally;
        _templateOutputFolder = templateOutputFolder;

        var configuredBuilder = Bind.BindFromContext<ExecutionBuilder>(Context, _validationResults,
            new BinderOptions { BindNonPublicProperties = true });
        DataSources = configuredBuilder.DataSources;
        Server = configuredBuilder.Server;
        Servers = configuredBuilder.Servers;
        Controller = configuredBuilder.Controller;
        Stubs = configuredBuilder.Stubs;
    }

    public ExecutionBuilder()
    {
        _validationResults = [];
        _scope = new ContainerBuilder().Build().BeginLifetimeScope();
        Context = new InternalContext
        {
            Logger = NullLogger.Instance,
            RootConfiguration = new ConfigurationBuilder().Build()
        };
        DataSources = [];
    }

    public ExecutionBuilder WithContext(InternalContext context)
    {
        Context = context;
        return this;
    }

    public ExecutionBuilder WithLogger(ILogger logger)
    {
        Context = CloneContext(logger: logger);
        return this;
    }

    public ExecutionBuilder WithRootConfiguration(IConfiguration configuration)
    {
        Context = CloneContext(rootConfiguration: configuration);
        return this;
    }

    public ExecutionBuilder WithExecutionMode(ExecutionMode executionMode)
    {
        _executionMode = executionMode;
        return this;
    }

    public ExecutionBuilder RunLocally(bool runLocally = true)
    {
        _runLocally = runLocally;
        return this;
    }

    public ExecutionBuilder WithTemplateOutputFolder(string? templateOutputFolder)
    {
        _templateOutputFolder = templateOutputFolder;
        return this;
    }

    public ExecutionBuilder CreateDataSource(DataSourceBuilder dataSourceBuilder)
    {
        ArgumentNullException.ThrowIfNull(dataSourceBuilder);

        var dataSourceName = dataSourceBuilder.Name
                             ?? throw new ArgumentException("Data source name is required", nameof(dataSourceBuilder));

        if (ReadDataSource(dataSourceName) != null)
            throw new InvalidOperationException($"Data source '{dataSourceName}' already exists.");

        DataSources = (DataSources ?? []).Append(dataSourceBuilder).ToArray();
        return this;
    }

    public DataSourceBuilder? ReadDataSource(string dataSourceName)
    {
        return (DataSources ?? []).FirstOrDefault(builder =>
            string.Equals(builder.Name, dataSourceName, StringComparison.OrdinalIgnoreCase));
    }

    public ExecutionBuilder UpdateDataSource(string dataSourceName, DataSourceBuilder dataSourceBuilder)
    {
        ArgumentNullException.ThrowIfNull(dataSourceBuilder);

        var updatedName = dataSourceBuilder.Name
                          ?? throw new ArgumentException("Data source name is required", nameof(dataSourceBuilder));

        var dataSources = (DataSources ?? []).ToList();
        var existingDataSourceIndex = dataSources.FindIndex(builder =>
            string.Equals(builder.Name, dataSourceName, StringComparison.OrdinalIgnoreCase));
        if (existingDataSourceIndex == -1)
            throw new KeyNotFoundException($"Data source '{dataSourceName}' was not found.");

        if (dataSources.Where((_, index) => index != existingDataSourceIndex).Any(builder =>
                string.Equals(builder.Name, updatedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Data source '{updatedName}' already exists.");

        dataSources[existingDataSourceIndex] = dataSourceBuilder;
        DataSources = dataSources.ToArray();
        return this;
    }

    public ExecutionBuilder DeleteDataSource(string dataSourceName)
    {
        var dataSources = (DataSources ?? []).ToList();
        var removedCount = dataSources.RemoveAll(builder =>
            string.Equals(builder.Name, dataSourceName, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
            throw new KeyNotFoundException($"Data source '{dataSourceName}' was not found.");

        DataSources = dataSources.ToArray();
        return this;
    }

    public ExecutionBuilder CreateStub(TransactionStubBuilder stubBuilder) => CreateStub(stubBuilder.Build());

    public ExecutionBuilder CreateStub(TransactionStubConfig stubConfig)
    {
        ArgumentNullException.ThrowIfNull(stubConfig);

        var stubName = stubConfig.Name
                       ?? throw new ArgumentException("Stub name is required", nameof(stubConfig));
        if (ReadStub(stubName) != null)
            throw new InvalidOperationException($"Stub '{stubName}' already exists.");

        Stubs = Stubs.Append(stubConfig).ToArray();
        return this;
    }

    public TransactionStubConfig? ReadStub(string stubName)
    {
        return Stubs.FirstOrDefault(stubConfig =>
            string.Equals(stubConfig.Name, stubName, StringComparison.OrdinalIgnoreCase));
    }

    public ExecutionBuilder UpdateStub(string stubName, Action<TransactionStubBuilder> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);

        var existingStub = ReadStub(stubName)
                           ?? throw new KeyNotFoundException($"Stub '{stubName}' was not found.");
        var updateBuilder = TransactionStubBuilder.FromConfig(existingStub);
        configureAction(updateBuilder);
        return UpdateStub(stubName, updateBuilder.Build());
    }

    public ExecutionBuilder UpdateStub(string stubName, TransactionStubBuilder stubBuilder)
        => UpdateStub(stubName, stubBuilder.Build());

    public ExecutionBuilder UpdateStub(string stubName, TransactionStubConfig stubConfig)
    {
        ArgumentNullException.ThrowIfNull(stubConfig);

        var updatedStubName = stubConfig.Name
                              ?? throw new ArgumentException("Stub name is required", nameof(stubConfig));

        var stubs = Stubs.ToList();
        var existingStubIndex = stubs.FindIndex(config =>
            string.Equals(config.Name, stubName, StringComparison.OrdinalIgnoreCase));
        if (existingStubIndex == -1)
            throw new KeyNotFoundException($"Stub '{stubName}' was not found.");

        if (stubs.Where((_, index) => index != existingStubIndex).Any(config =>
                string.Equals(config.Name, updatedStubName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Stub '{updatedStubName}' already exists.");

        stubs[existingStubIndex] = stubConfig;
        Stubs = stubs.ToArray();
        return this;
    }

    public ExecutionBuilder DeleteStub(string stubName)
    {
        var stubs = Stubs.ToList();
        var removedCount = stubs.RemoveAll(stubConfig =>
            string.Equals(stubConfig.Name, stubName, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
            throw new KeyNotFoundException($"Stub '{stubName}' was not found.");

        Stubs = stubs.ToArray();
        return this;
    }

    public ServerConfig? ReadServer() => Server;

    public IReadOnlyList<ServerConfig> ReadServers() => ResolveConfiguredServers().ToArray();

    public ExecutionBuilder CreateServer(ServerConfig serverConfig)
    {
        ArgumentNullException.ThrowIfNull(serverConfig);

        if (Server != null || Servers.Length > 0)
            throw new InvalidOperationException("Server is already configured.");

        Server = serverConfig;
        return this;
    }

    public ExecutionBuilder ReplaceServer(ServerConfig serverConfig)
    {
        ArgumentNullException.ThrowIfNull(serverConfig);

        Server = serverConfig;
        Servers = [];
        return this;
    }

    public ExecutionBuilder AddServer(ServerConfig serverConfig)
    {
        ArgumentNullException.ThrowIfNull(serverConfig);

        Servers = ResolveConfiguredServers().Append(serverConfig).ToArray();
        Server = null;
        return this;
    }

    public ExecutionBuilder ReplaceServers(params ServerConfig[] serverConfigs)
    {
        ArgumentNullException.ThrowIfNull(serverConfigs);

        Servers = serverConfigs.ToArray();
        Server = null;
        return this;
    }

    public ExecutionBuilder UpdateServer(Action<ServerConfig> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        if (Server == null)
            throw new InvalidOperationException("Single server configuration is not configured.");

        configureAction(Server);
        return this;
    }

    public ControllerConfig? ReadController() => Controller;

    public ExecutionBuilder CreateController(ControllerConfig controllerConfig)
    {
        if (Controller != null)
            throw new InvalidOperationException("Controller is already configured.");

        Controller = controllerConfig;
        return this;
    }

    public ExecutionBuilder ReplaceController(ControllerConfig? controllerConfig)
    {
        Controller = controllerConfig;
        return this;
    }

    public ExecutionBuilder UpdateController(Action<ControllerConfig> configureAction)
    {
        ArgumentNullException.ThrowIfNull(configureAction);
        if (Controller == null)
            throw new InvalidOperationException("Controller is not configured.");

        configureAction(Controller);
        return this;
    }

    public ExecutionBuilder DeleteController()
    {
        Controller = null;
        return this;
    }

    private void LoadContextScopeDependencies()
    {
        // Build a dedicated child scope for hook discovery and object creation so the execution can
        // resolve runtime dependencies without leaking the temporary bootstrap scope.
        var contextScope = _scope.BeginLifetimeScope(containerBuilder =>
        {
            containerBuilder.RegisterInstance(Context).As<InternalContext>().SingleInstance();
            containerBuilder.RegisterInstance(Context).As<Context>().SingleInstance();
            containerBuilder.RegisterInstance(new ByNameObjectCreator(Context.Logger)).As<IByNameObjectCreator>();

            containerBuilder.Register<IComponentContext, IEnumerable<HookData<IGenerator>>>(_ =>
                (DataSources ?? []).Select(dataSourceConfig => new HookData<IGenerator>
                {
                    Type = GetDataSourceGeneratorName(dataSourceConfig),
                    Configuration = GetDataSourceGeneratorConfiguration(dataSourceConfig),
                    Name = dataSourceConfig.Name!
                })
            ).InstancePerLifetimeScope();

            containerBuilder.Register<IComponentContext, IEnumerable<HookData<ITransactionProcessor>>>(_ =>
                Stubs.Select(stubConfig => new HookData<ITransactionProcessor>
                {
                    Type = ResolveProcessorTypeName(stubConfig.Processor!),
                    Configuration = stubConfig.ProcessorConfiguration,
                    Name = stubConfig.Name!
                })
            ).InstancePerLifetimeScope();

            containerBuilder.RegisterModule(new HooksLoaderModule<IGenerator>(_validationResults));
            containerBuilder.RegisterModule(new HooksLoaderModule<ITransactionProcessor>(_validationResults));
        });

        _scope = contextScope;
    }

    private IImmutableList<TransactionStub> BuildStubs(IImmutableList<DataSource> dataSources)
    {
        var stubFactory = new StubFactory(Context, Stubs, _scope.Resolve<IList<KeyValuePair<string, ITransactionProcessor>>>());
        return new StubsLogic(stubFactory, dataSources).Build();
    }

    public override BaseExecution Build()
    {
        _validationResults.Clear();

        Context.Logger.LogInformation(
            "Started building QaaS.Mocker with execution mode {ExecutionMode}",
            _executionMode);

        LoadContextScopeDependencies();

        _ = ValidationUtils.TryValidateObjectRecursive(this, _validationResults);
        _validationResults.AddRange(Validate(new ValidationContext(this)));
        if (_validationResults.Any())
        {
            Context.Logger.LogCritical("Configurations are not valid. The validation results are: \n- " +
                                       string.Join("\n- ", _validationResults.Select(result => result.ErrorMessage)));
            throw new InvalidConfigurationsException("Configurations are not valid");
        }

        var dataSources = BuildDataSources().ToImmutableList();
        var stubs = BuildStubs(dataSources);
        var configuredServers = ResolveConfiguredServers();
        var server = new ServerFactory(Context, configuredServers).Build(dataSources, stubs);
        var controller = new ControllerFactory(Context, Controller).Build(server.State);
        // Log the assembled runtime graph once the builder has resolved every major dependency.
        Context.Logger.LogInformation(
            "Resolved runtime graph with {DataSourceCount} data source(s), {StubCount} stub(s), {ServerCount} server(s) [{ServerTypes}], and controller enabled: {ControllerEnabled}",
            dataSources.Count,
            stubs.Count,
            configuredServers.Count,
            ResolveServerTypesSummary(configuredServers),
            controller != null);

        var serverLogic = new ServerLogic(server);
        var templateLogic = new TemplateLogic(Context, _templateOutputFolder);
        var controllerLogic = controller == null ? null : new ControllerLogic(controller);

        Context.Logger.LogInformation(
            "Finished building QaaS.Mocker with execution mode {ExecutionMode}",
            _executionMode);

        return new Execution(_executionMode, Context, _runLocally)
        {
            ServerLogic = serverLogic,
            ControllerLogic = controllerLogic,
            TemplateLogic = templateLogic
        };
    }

    private static string GetDataSourceGeneratorName(DataSourceBuilder dataSourceBuilder)
    {
        return DataSourceGeneratorProperty.GetValue(dataSourceBuilder)?.ToString() ??
               throw new InvalidOperationException(
                   "Could not resolve generator name from DataSource configuration. Ensure DataSources are configured correctly.");
    }

    private static string ResolveProcessorTypeName(string configuredProcessor)
    {
        if (string.IsNullOrWhiteSpace(configuredProcessor) || configuredProcessor.Contains('.'))
            return configuredProcessor;

        var matchingProcessorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(GetLoadableTypes)
            .Where(type =>
                type is { IsAbstract: false, IsInterface: false } &&
                typeof(ITransactionProcessor).IsAssignableFrom(type) &&
                string.Equals(type.Name, configuredProcessor, StringComparison.Ordinal))
            .Select(type => type.FullName)
            .Where(typeName => !string.IsNullOrWhiteSpace(typeName))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return matchingProcessorTypes.Length == 1
            ? matchingProcessorTypes[0]!
            : configuredProcessor;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }

    private static IConfiguration GetDataSourceGeneratorConfiguration(DataSourceBuilder dataSourceBuilder)
    {
        return (IConfiguration?)DataSourceGeneratorConfigurationProperty.GetValue(dataSourceBuilder) ??
               new ConfigurationBuilder().Build();
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasSingleServer = Server != null;
        var hasMultipleServers = Servers.Length > 0;

        if (!hasSingleServer && !hasMultipleServers)
        {
            yield return new ValidationResult(
                "Either 'Server' or 'Servers' must be configured.",
                [nameof(Server), nameof(Servers)]);
            yield break;
        }

        if (hasSingleServer && hasMultipleServers)
        {
            yield return new ValidationResult(
                "Configure either 'Server' or 'Servers', not both.",
                [nameof(Server), nameof(Servers)]);
            yield break;
        }

        if (!hasMultipleServers)
            yield break;

        var duplicateActionNames = ResolveActionNames(Servers)
            .GroupBy(actionName => actionName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(actionName => actionName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (duplicateActionNames.Length > 0)
        {
            yield return new ValidationResult(
                "Action names must be unique across 'Servers'. Duplicates: " +
                string.Join(", ", duplicateActionNames),
                [nameof(Servers)]);
        }
    }

    private IReadOnlyList<ServerConfig> ResolveConfiguredServers()
    {
        if (Servers.Length > 0)
            return Servers;
        return Server == null ? [] : [Server];
    }

    private static IEnumerable<string> ResolveActionNames(IEnumerable<ServerConfig> serverConfigs)
    {
        foreach (var serverConfig in serverConfigs)
        {
            if (serverConfig.Http?.Endpoints != null)
            {
                foreach (var actionName in serverConfig.Http.Endpoints
                             .SelectMany(endpoint => endpoint.Actions)
                             .Select(action => action.Name)
                             .Where(actionName => !string.IsNullOrWhiteSpace(actionName)))
                {
                    yield return actionName!;
                }
            }

            if (serverConfig.Grpc?.Services != null)
            {
                foreach (var actionName in serverConfig.Grpc.Services
                             .SelectMany(service => service.Actions.Select(action => action.Name ?? $"{service.ServiceName}.{action.RpcName}")))
                {
                    yield return actionName;
                }
            }

            if (serverConfig.Socket?.Endpoints != null)
            {
                foreach (var actionName in serverConfig.Socket.Endpoints
                             .Select(endpoint => endpoint.Action?.Name)
                             .Where(actionName => !string.IsNullOrWhiteSpace(actionName)))
                {
                    yield return actionName!;
                }
            }
        }
    }

    private static string ResolveServerTypesSummary(IEnumerable<ServerConfig> serverConfigs)
    {
        return string.Join(", ", serverConfigs.Select(serverConfig => serverConfig.Type));
    }

    private void EnsureDefaultMetaData()
    {
        try
        {
            _ = Context.GetMetaDataFromContext();
        }
        catch (KeyNotFoundException)
        {
            Context.InsertValueIntoGlobalDictionary(Context.GetMetaDataPath(), new MetaDataConfig());
        }
        catch (InvalidOperationException)
        {
            Context.InsertValueIntoGlobalDictionary(Context.GetMetaDataPath(), new MetaDataConfig());
        }
    }

    private InternalContext CloneContext(ILogger? logger = null, IConfiguration? rootConfiguration = null)
    {
        return new InternalContext
        {
            Logger = logger ?? Context.Logger,
            RootConfiguration = rootConfiguration ?? Context.RootConfiguration,
            ExecutionData = Context.ExecutionData,
            CaseName = Context.CaseName,
            ExecutionId = Context.ExecutionId,
            InternalRunningSessions = Context.InternalRunningSessions,
            InternalGlobalDict = Context.InternalGlobalDict
        };
    }
}
