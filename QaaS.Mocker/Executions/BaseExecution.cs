using System.ComponentModel.DataAnnotations;
using Autofac;
using Autofac.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.Providers;
using QaaS.Framework.Providers.Modules;
using QaaS.Framework.Providers.ObjectCreation;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Framework.SDK.Hooks.Generator;
using QaaS.Framework.SDK.Hooks.Processor;
using QaaS.Framework.Serialization;
using QaaS.Mocker.Controller.Modules;
using QaaS.Mocker.Modules;
using QaaS.Mocker.Servers.Modules;
using QaaS.Mocker.Stubs.ConfigurationObjects;
using QaaS.Mocker.Stubs.Modules;

namespace QaaS.Mocker.Executions;

public abstract class BaseExecution(Context context, bool mustBeValid)
{
    private readonly List<ValidationResult> _validationResults = [];
    private readonly ContainerBuilder _containerBuilder = new();
    protected readonly Context Context = context;

    /// <summary>
    /// Whether or not the configurations/providers must be valid in order for the run to continue
    /// </summary>
    private readonly bool _mustBeValid = mustBeValid;

    /// <summary>
    /// Run runnable with given configurations
    /// </summary>
    /// <returns> returns success status code - 0 is successful anything else means failure </returns>
    public int Run()
    {
        var programStartTime = DateTime.UtcNow;
        using var container = BuildContainer(programStartTime);
        var registeredTypes = container.ComponentRegistry.Registrations
            .SelectMany(r => r.Services)
            .OfType<IServiceWithType>().Select(s => s.ServiceType.FullName).ToArray();
        Context.Logger.LogDebug("Facade container registered types are: {RegisteredTypes}",
            new object[] { registeredTypes });

        using var scope = container.BeginLifetimeScope();
        ValidateScope(scope);
        var executionResult = Execute(scope);
        return executionResult;
    }

    private IContainer BuildContainer(DateTime programStartTime)
    {
        _containerBuilder.Register(_ => programStartTime).As<DateTime>().SingleInstance();
        _containerBuilder.RegisterInstance(Context).As<Context>().SingleInstance();
        _containerBuilder.RegisterInstance(new BinderOptions()).As<BinderOptions>().SingleInstance();
        _containerBuilder.RegisterInstance(new ByNameObjectCreator(Context.Logger)).As<IByNameObjectCreator>()
            .SingleInstance();
        _containerBuilder.RegisterModule(new ConfigurationsModule(_validationResults)); // Loads configurations lazily
        _containerBuilder.Register<IComponentContext, IEnumerable<HookData<IGenerator>>>(context => context
            .Resolve<DataSources.ConfigurationObjects.Configurations>().DataSources
            .Select(dataSourceConfig => new HookData<IGenerator>
            {
                Type = dataSourceConfig.Generator!,
                Configuration = dataSourceConfig.GeneratorSpecificConfiguration,
                Name = dataSourceConfig.Name!
            })
        ).InstancePerLifetimeScope(); // Loads all IGenerator hooks
        _containerBuilder.Register<IComponentContext, IEnumerable<HookData<ITransactionProcessor>>>(context => context
            .Resolve<Configurations>().Stubs
            .Select(dataSourceConfig => new HookData<ITransactionProcessor>
            {
                Type = dataSourceConfig.Processor!,
                Configuration = dataSourceConfig.ProcessorSpecificConfiguration,
                Name = dataSourceConfig.Name!
            })
        ).InstancePerLifetimeScope(); // Loads all ITransactionProcessor hooks
        _containerBuilder.RegisterModule(new HooksLoaderModule<IGenerator>(_validationResults)); // Loads all IGenerator hooks
        _containerBuilder.RegisterModule(new HooksLoaderModule<ITransactionProcessor>(_validationResults)); // Loads all ITransactionProcessor hooks
        _containerBuilder.RegisterModule(new DataSourcesModule()); // Load generators handler facade
        _containerBuilder.RegisterModule(new StubsModule()); // Load stubs facade
        _containerBuilder.RegisterModule(new ServersModule()); // Load sessions facade
        _containerBuilder.RegisterModule(new ControllerModule()); // Load controller module
        return _containerBuilder.Build();
    }

    private void ValidateScope(ILifetimeScope scope)
    {
        try
        {
            scope.Resolve<DataSources.ConfigurationObjects.Configurations>();
            scope.Resolve<Servers.ConfigurationObjects.Configurations>();
            scope.Resolve<Controller.ConfigurationObjects.Configurations>();
            scope.Resolve<IList<KeyValuePair<string, IGenerator>>>();
            scope.Resolve<IList<KeyValuePair<string, ITransactionProcessor>>>();
        }
        catch (Exception exception)
        {
            _validationResults.Add(
                new ValidationResult($"Encountered exception when initializing facades: \n{exception}"));
        }

        if (_validationResults.Count != 0)
        {
            Context.Logger.LogCritical("Configurations are not valid. The validation results are: \n- " +
                                       string.Join("\n- ", _validationResults.Select(result => result.ErrorMessage)));
            if (_mustBeValid) throw new InvalidConfigurationsException("Configurations are not valid");
        }
        
        Context.Logger.LogInformation("Configurations and provided hooks loaded and validated successfully");
    }

    protected abstract int Execute(ILifetimeScope scope);
}