using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Configurations.CustomValidationAttributes;
using QaaS.Framework.Executions;
using QaaS.Framework.Providers;
using QaaS.Framework.Providers.Modules;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.DataSourceObjects;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Mocker.Controller.ConfigurationObjects;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers.ConfigurationObjects;
using QaaS.Mocker.Stubs.ConfigurationObjects;

namespace QaaS.Mocker;

public class ExecutionBuilder : BaseExecutionBuilder<InternalContext, ExecutionData>
{
    [UniquePropertyInEnumerable(nameof(TransactionStubConfig.Name)),
     Description("List of transaction stubs that can be used for server actions." +
                 "They provide processing functionality to exercise transaction data.")]
    public TransactionStubConfig[] Stubs { get; set; } = [];

    [Description("The server mocker instance to run.")]
    public ServerConfig Server { get; set; } = new();

    [Description("The server mocker controller configuration")]
    public ControllerConfig? Controller { get; set; }

    private ILifetimeScope _scope;
    private readonly List<ValidationResult> _validationResults;

    private readonly ExecutionMode _executionMode;
    private readonly bool _runLocally;

    protected override IEnumerable<DataSource> BuildDataSources()
    {
        var configuredDataSources = DataSources ?? [];
        var dataSources = configuredDataSources.Select(dataSourceBuilder => dataSourceBuilder.Register())
            .ToImmutableList();
        var resolvedDataSources = configuredDataSources.Select(dataSourceBuilder =>
            dataSourceBuilder.Build(Context, dataSources, _scope.Resolve<IList<KeyValuePair<string, IGenerator>>>()));
        return resolvedDataSources;
    }

    internal ExecutionBuilder(InternalContext context, ExecutionMode executionMode, bool runLocally) : this()
    {
        Context = context;
        _executionMode = executionMode;
        _runLocally = runLocally;
        var blankRunBuilderFromContext = Bind.BindFromContext<ExecutionBuilder>(Context, _validationResults,
            new BinderOptions() { BindNonPublicProperties = true });
        DataSources = blankRunBuilderFromContext.DataSources;
        Server = blankRunBuilderFromContext.Server;
        Controller = blankRunBuilderFromContext.Controller;
        Stubs = blankRunBuilderFromContext.Stubs;
    }

    public ExecutionBuilder()
    {
        _validationResults = [];
        _scope = new ContainerBuilder().Build().BeginLifetimeScope();
    }

    /// <summary>
    ///     Loads the <see cref="ExecutionBuilder" /> scope with all context's dependencies
    /// </summary>
    private void LoadContextScopeDependencies()
    {
        var contextScope = _scope.BeginLifetimeScope(containerBuilder =>
        {
            // Loads context into scope
            containerBuilder.RegisterInstance(Context).As<InternalContext>().SingleInstance();
            containerBuilder.RegisterInstance(Context).As<Context>().SingleInstance();
            containerBuilder.RegisterInstance(new ByNameObjectCreator(Context.Logger)).As<IByNameObjectCreator>();

            containerBuilder.Register<IComponentContext, IEnumerable<HookData<IGenerator>>>(_ =>
                DataSources?.Select(dataSourceConfig => new HookData<IGenerator>
                {
                    Type = dataSourceConfig.Generator!,
                    Configuration = dataSourceConfig.GeneratorConfiguration,
                    Name = dataSourceConfig.Name!
                }) ?? []
            ).InstancePerLifetimeScope(); // Loads all IGenerator hooks
            containerBuilder.Register<IComponentContext, IEnumerable<HookData<ITransactionProcessor>>>(_ =>
                Stubs
                    .Select(dataSourceConfig => new HookData<ITransactionProcessor>
                    {
                        Type = dataSourceConfig.Processor!,
                        Configuration = dataSourceConfig.ProcessorSpecificConfiguration,
                        Name = dataSourceConfig.Name!
                    })
            ).InstancePerLifetimeScope(); // Loads all ITransactionProcessor hooks
            containerBuilder.RegisterModule(
                new HooksLoaderModule<IGenerator>(_validationResults)); // Loads all IGenerator hooks
            containerBuilder.RegisterModule(
                new HooksLoaderModule<ITransactionProcessor>(
                    _validationResults)); // Loads all ITransactionProcessor hooks

            // // loads logics
            // containerBuilder.RegisterType<DataSourceLogic>().As<DataSourceLogic>();
            // containerBuilder.RegisterType<SessionLogic>().As<SessionLogic>();
            // containerBuilder.RegisterType<StorageLogic>().As<StorageLogic>();
            // containerBuilder.RegisterType<AssertionLogic>().As<AssertionLogic>();
            // containerBuilder.RegisterType<ReportLogic>().As<ReportLogic>();
            // containerBuilder.RegisterType<TemplateLogic>().As<TemplateLogic>();
        });

        _scope = contextScope;
    }

    public override BaseExecution Build()
    {
        Context.Logger.LogInformation(
            "Started building Mocker with execution mode {ExecutionMode}", _executionMode);
        
        // loads all hooks & logics validate them
        LoadContextScopeDependencies();
        
        // validate configuration
        _ = ValidationUtils.TryValidateObjectRecursive(this, _validationResults);
        
        if (_validationResults.Any())
        {
            Context.Logger.LogCritical("Configurations are not valid. The validation results are: \n- " +
                                       string.Join("\n- ", _validationResults.Select(result => result.ErrorMessage)));
            throw new InvalidConfigurationsException("Configurations are not valid");
        }

        // TODO: implement Logics/Factories loading with autofac by the built properties
        
        Context.Logger.LogInformation(
            "Finished building Mocker with execution mode {ExecutionMode}", _executionMode);
        
        // bind back context onto the executionBuilder object
        return new Execution(_executionMode, Context, _runLocally);
    }
}