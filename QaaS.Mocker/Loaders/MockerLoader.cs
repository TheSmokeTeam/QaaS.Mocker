using Autofac;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Loaders;

/// <summary>
/// Loads CLI options into an execution-ready <see cref="Mocker"/> instance.
/// </summary>
public class MockerLoader : BaseLoader<MockerOptions, Mocker>
{
    private readonly ILifetimeScope _runScope;

    /// <summary>
    /// Initializes a new loader instance from parsed CLI options.
    /// </summary>
    /// <param name="options">Command-line options used to load execution context.</param>
    /// <param name="executionId">Optional execution identifier override.</param>
    public MockerLoader(MockerOptions options, string? executionId = null) : base(options, executionId)
    {
        _runScope = InitializeScope();
    }

    /// <summary>
    /// Builds an <see cref="InternalContext"/> from configuration file, overwrites, and environment resolution.
    /// </summary>
    /// <returns>The loaded internal context.</returns>
    private InternalContext GetLoadedContext()
    {
        if (string.IsNullOrWhiteSpace(Options.ConfigurationFile))
            throw new ArgumentException("Configuration file path is required.", nameof(Options.ConfigurationFile));

        var contextBuilder = new ContextBuilder(_runScope.Resolve<IConfigurationBuilder>());

        contextBuilder.SetLogger(Logger);
        contextBuilder.SetConfigurationFile(Options.ConfigurationFile);
        foreach (var overwriteFile in Options.OverwriteFiles ?? [])
            contextBuilder.WithOverwriteFile(overwriteFile);
        foreach (var overwriteArgument in Options.OverwriteArguments ?? [])
            contextBuilder.WithOverwriteArgument(overwriteArgument);
        if (!Options.DontResolveWithEnvironmentVariables)
            contextBuilder.WithEnvironmentVariableResolution();
        return contextBuilder.BuildInternal();
    }

    /// <summary>
    /// Maps loaded context and run options to an <see cref="ExecutionBuilder"/>.
    /// </summary>
    /// <param name="context">Loaded context.</param>
    /// <returns>Configured execution builder.</returns>
    private ExecutionBuilder LoadContextToExecutionBuilder(InternalContext context)
    {
        if (!Options.ExecutionMode.HasValue)
            throw new ArgumentException("Execution mode is required.", nameof(Options.ExecutionMode));

        var runBuilder = new ExecutionBuilder(context, Options.ExecutionMode!.Value, Options.RunLocally,
            Options.TemplatesOutputFolder);
        return runBuilder;
    }

    /// <summary>
    /// Creates a dedicated lifetime scope for loader dependencies.
    /// </summary>
    /// <returns>A lifetime scope used during context loading.</returns>
    private ILifetimeScope InitializeScope()
    {
        return new ContainerBuilder().Build().BeginLifetimeScope(scope =>
        {
            // Must not be single instance so it builds a new configuration builder for every context
            scope.RegisterType<ConfigurationBuilder>().As<IConfigurationBuilder>();
        });
    }

    /// <summary>
    /// Creates the runner instance from current loader options.
    /// </summary>
    /// <returns>A runnable <see cref="Mocker"/> instance.</returns>
    public override Mocker GetLoadedRunner() => new(LoadContextToExecutionBuilder(GetLoadedContext()));
}
