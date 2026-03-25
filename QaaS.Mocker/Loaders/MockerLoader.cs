using System.Collections;
using Autofac;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QaaS.Framework.Configurations;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Loaders;

/// <summary>
/// Loads CLI options into an execution-ready <see cref="MockerRunner"/> instance.
/// </summary>
public class MockerLoader<TOptions> : BaseLoader<TOptions, MockerRunner>, IDisposable
    where TOptions : MockerOptions
{
    private readonly ILifetimeScope _runScope;
    private static readonly string[] SupportedEnvironmentSeparators = [":", "__"];
    private readonly Lazy<IReadOnlyList<IExecutionBuilderConfigurator>> _executionBuilderConfigurators;
    private bool _missingConfigurationFileWarningLogged;

    /// <summary>
    /// Initializes a new loader instance from parsed CLI options.
    /// </summary>
    /// <param name="options">Command-line options used to load execution context.</param>
    /// <param name="executionId">Optional execution identifier override.</param>
    public MockerLoader(TOptions options, string? executionId = null) : base(options, executionId)
    {
        _runScope = InitializeScope();
        _executionBuilderConfigurators = new Lazy<IReadOnlyList<IExecutionBuilderConfigurator>>(
            DiscoverExecutionBuilderConfigurators);
    }

    /// <summary>
    /// Builds an <see cref="InternalContext"/> from configuration file, overwrites, and environment resolution.
    /// </summary>
    /// <returns>The loaded internal context.</returns>
    protected virtual InternalContext GetLoadedContext()
    {
        if (string.IsNullOrWhiteSpace(Options.ConfigurationFile))
            throw new ArgumentException("Configuration file path is required.", nameof(Options.ConfigurationFile));

        var contextBuilder = new ContextBuilder(_runScope.Resolve<IConfigurationBuilder>());
        var resolvedConfigurationFilePath = ResolveConfigurationFilePath();

        contextBuilder.SetLogger(Logger);
        if (ShouldLoadConfigurationFile())
            contextBuilder.SetConfigurationFile(resolvedConfigurationFilePath);
        foreach (var overwriteFile in Options.OverwriteFiles ?? [])
            contextBuilder.WithOverwriteFile(overwriteFile);
        foreach (var overwriteFolder in Options.OverwriteFolders ?? [])
            contextBuilder.WithOverwriteFolder(overwriteFolder);
        foreach (var overwriteArgument in Options.OverwriteArguments ?? [])
            contextBuilder.WithOverwriteArgument(overwriteArgument);
        if (!Options.DontResolveWithEnvironmentVariables)
            ApplyEnvironmentOverrides(contextBuilder);
        return contextBuilder.BuildInternal();
    }

    /// <summary>
    /// Maps loaded context and run options to an <see cref="ExecutionBuilder"/>.
    /// </summary>
    /// <param name="context">Loaded context.</param>
    /// <returns>Configured execution builder.</returns>
    protected virtual ExecutionBuilder LoadContextToExecutionBuilder(InternalContext context)
    {
        var runBuilder = new ExecutionBuilder(context, Options.GetExecutionMode(), Options.RunLocally,
            Options.TemplatesOutputFolder);

        foreach (var configurator in _executionBuilderConfigurators.Value)
        {
            Logger.LogDebug(
                "Applying mocker execution configurator {ConfiguratorType}",
                configurator.GetType().FullName);
            configurator.Configure(runBuilder);
        }

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
    /// <returns>A runnable <see cref="MockerRunner"/> instance.</returns>
    public override MockerRunner GetLoadedRunner() => new(LoadContextToExecutionBuilder(GetLoadedContext()));

    public void Dispose()
    {
        _runScope.Dispose();
    }

    /// <summary>
    /// Applies only explicit configuration-like environment variables so unrelated IDE/runtime
    /// variables do not pollute the execution builder.
    /// </summary>
    private void ApplyEnvironmentOverrides(ContextBuilder contextBuilder)
    {
        var environmentVariables = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .Select(entry => new KeyValuePair<string?, string?>(
                entry.Key?.ToString(),
                entry.Value?.ToString()));

        ApplyEnvironmentOverrides(contextBuilder, environmentVariables, Logger);
    }

    internal static void ApplyEnvironmentOverrides(
        ContextBuilder contextBuilder,
        IEnumerable<KeyValuePair<string?, string?>> environmentVariables,
        ILogger logger)
    {
        var appliedOverrides = 0;
        foreach (var environmentVariable in environmentVariables)
        {
            var environmentVariableName = environmentVariable.Key;
            var environmentVariableValue = environmentVariable.Value;
            if (string.IsNullOrWhiteSpace(environmentVariableName) || environmentVariableValue == null)
                continue;

            if (!TryMapEnvironmentVariableToConfigurationPath(environmentVariableName, out var configurationPath))
                continue;

            contextBuilder.WithOverwriteArgument($"{configurationPath}={environmentVariableValue}");
            appliedOverrides++;
        }

        if (appliedOverrides > 0)
            logger.LogInformation("Applied {EnvironmentOverrideCount} environment override(s)", appliedOverrides);
    }

    private static bool TryMapEnvironmentVariableToConfigurationPath(
        string environmentVariableName,
        out string configurationPath)
    {
        foreach (var separator in SupportedEnvironmentSeparators)
        {
            var pathSegments = environmentVariableName
                .Split([separator], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pathSegments.Length == 0)
                continue;

            if (!Constants.ConfigurationSectionNames.Any(sectionName =>
                    string.Equals(sectionName, pathSegments[0], StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            configurationPath = string.Join(':', pathSegments);
            return true;
        }

        configurationPath = string.Empty;
        return false;
    }

    protected virtual IReadOnlyList<IExecutionBuilderConfigurator> DiscoverExecutionBuilderConfigurators()
    {
        return ExecutionBuilderConfiguratorLoader.Load(Logger);
    }

    private bool ShouldLoadConfigurationFile()
    {
        if (string.IsNullOrWhiteSpace(Options.ConfigurationFile))
            return false;

        if (PathUtils.IsPathHttpUrl(Options.ConfigurationFile))
            return true;

        var resolvedConfigurationFilePath = ResolveConfigurationFilePath();
        if (File.Exists(resolvedConfigurationFilePath))
            return true;

        if (_executionBuilderConfigurators.Value.Count == 0)
            return true;

        if (!_missingConfigurationFileWarningLogged)
        {
            Logger.LogWarning(
                "Configuration file {ConfigurationFile} was not found at {ResolvedConfigurationFilePath}. Continuing with {ConfiguratorCount} discovered code configurator(s).",
                Options.ConfigurationFile,
                resolvedConfigurationFilePath,
                _executionBuilderConfigurators.Value.Count);
            _missingConfigurationFileWarningLogged = true;
        }

        return false;
    }

    private string ResolveConfigurationFilePath()
    {
        if (string.IsNullOrWhiteSpace(Options.ConfigurationFile))
            throw new ArgumentException("Configuration file path is required.", nameof(Options.ConfigurationFile));

        if (PathUtils.IsPathHttpUrl(Options.ConfigurationFile))
            return Options.ConfigurationFile;

        if (Path.IsPathRooted(Options.ConfigurationFile))
            return Options.ConfigurationFile;

        var currentDirectoryConfigurationFilePath =
            Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, Options.ConfigurationFile));

        if (!string.Equals(Options.ConfigurationFile, Constants.DefaultMockerConfigurationFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            return currentDirectoryConfigurationFilePath;
        }

        if (File.Exists(currentDirectoryConfigurationFilePath))
            return currentDirectoryConfigurationFilePath;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, Options.ConfigurationFile));
    }
}

/// <summary>
/// Loads CLI options into a custom <typeparamref name="TRunner" /> instance.
/// </summary>
public class MockerLoader<TRunner, TOptions> : MockerLoader<TOptions>
    where TRunner : MockerRunner
    where TOptions : MockerOptions
{
    /// <summary>
    /// Initializes a new loader instance from parsed CLI options.
    /// </summary>
    public MockerLoader(TOptions options, string? executionId = null) : base(options, executionId)
    {
    }

    /// <summary>
    /// Creates the runner instance from current loader options.
    /// </summary>
    /// <returns>A runnable <typeparamref name="TRunner" /> instance.</returns>
    public new TRunner GetLoadedRunner()
    {
        return Bootstrap.CreateRunner<TRunner>(LoadContextToExecutionBuilder(GetLoadedContext()));
    }
}
