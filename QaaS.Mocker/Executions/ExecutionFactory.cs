using System.ComponentModel.DataAnnotations;
using Autofac;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Configurations;
using QaaS.Framework.Configurations.ConfigurationBuilderExtensions;
using QaaS.Framework.Configurations.CustomExceptions;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Options;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace QaaS.Mocker.Executions;

/// <summary>
/// The base executor with all functionality any executor requires
/// </summary>
public class ExecutionFactory
{
    /// <summary>
    /// The executor's logger
    /// </summary>
    private readonly ILogger _logger;
    
    /// <summary>
    /// The options to execute with
    /// </summary>
    private readonly Options.MockerOptions _mockerOptions;
    
    /// <summary>
    /// The scope the executor runs in, contains all built containers
    /// </summary>
    private readonly ILifetimeScope _scope;

    private readonly Context _context; 

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="mockerOptions"> The options to execute with </param>
    public ExecutionFactory(Options.MockerOptions mockerOptions)
    {
        ValidateOptions(mockerOptions);
        _mockerOptions = mockerOptions;
        _logger = _mockerOptions.LoggerConfigurationFilePath != null
            // Custom logger is provided
            ? CreateLoggerFromConfiguration(
                CreateLoggerConfigurationFromLoggerConfigurationFile(_mockerOptions.LoggerConfigurationFilePath, _mockerOptions.LoggerLevel))
            // Default logger logs to console
            : CreateLoggerFromConfiguration(
                new LoggerConfiguration().MinimumLevel.Is(_mockerOptions.LoggerLevel ?? LogEventLevel.Information)
                .WriteTo.Console());
        _scope = BuildLifeTimeScope();
        _context = BuildContext();
    }

    private static LoggerConfiguration CreateLoggerConfigurationFromLoggerConfigurationFile(string loggerConfigurationFilePath,
        LogEventLevel? loggerLevel) =>
        loggerLevel != null
                ? new LoggerConfiguration()
                    .ReadFrom.Configuration(new ConfigurationBuilder().AddYaml(loggerConfigurationFilePath).Build())
                    .MinimumLevel.Is(loggerLevel.Value)
                : new LoggerConfiguration()
                    .ReadFrom.Configuration(new ConfigurationBuilder().AddYaml(loggerConfigurationFilePath).Build());
    
    private ILogger CreateLoggerFromConfiguration(LoggerConfiguration loggerConfiguration) => 
        new SerilogLoggerFactory(loggerConfiguration.CreateLogger()).CreateLogger(GetType().Name);
    
    private static void ValidateOptions(Options.MockerOptions mockerOptions)
    {
        var commandLineValidationResults = new List<ValidationResult>();
        ValidationUtils.TryValidateObjectRecursive(mockerOptions, commandLineValidationResults);
        if (commandLineValidationResults.Any())
            throw new InvalidConfigurationsException(
                "Given command arguments are not valid. The validation results are: \n- " +
                string.Join("\n- ", commandLineValidationResults.Select(result =>
                    result.ErrorMessage)));
    }
    
    private ILifetimeScope BuildLifeTimeScope()
    {
        var containerBuilder = new ContainerBuilder();
        AddRegistriesToContainer(containerBuilder);
        return containerBuilder.Build().BeginLifetimeScope();
    }
    
    
    /// <summary>
    /// Register additional items to the container before building it
    /// </summary>
    private void AddRegistriesToContainer(ContainerBuilder containerBuilder)
    {
        // Must not be single instance so it builds a new configuration builder for every context
        containerBuilder.RegisterType<ConfigurationBuilder>().As<IConfigurationBuilder>();
    }
    

    /// <summary>
    /// Builds Context from scope.
    /// </summary>
    /// <returns>The built Context</returns>
    private Context BuildContext()
    {
        // referenceResolutionPaths & uniqueIdPathRegexes
        var contextBuilder = new ContextBuilder(_scope.Resolve<IConfigurationBuilder>()); 
        
        contextBuilder.SetLogger(_logger);
        contextBuilder.SetConfigurationFile(_mockerOptions.ConfigurationFile!);
        foreach (var overwriteFile in _mockerOptions.OverwriteFiles)
            contextBuilder.WithOverwriteFile(overwriteFile);
        foreach (var overwriteArgument in _mockerOptions.OverwriteArguments)
            contextBuilder.WithOverwriteArgument(overwriteArgument);
        if (!_mockerOptions.DontResolveWithEnvironmentVariables) 
            contextBuilder.WithEnvironmentVariableResolution();
        return contextBuilder.BuildInternal();
    }

    /// <summary>
    /// Builds the relevant execution.
    /// </summary>
    public BaseExecution Build()
    {
        return _mockerOptions.ExecutionMode switch
        {
            ExecutionMode.Run => new RunExecution(_context, _mockerOptions.RunLocally),
            ExecutionMode.Lint => new LintExecution(_context),
            ExecutionMode.Template => new TemplateExecution(_context, _mockerOptions.TemplatesOutputFolder),
            _ => throw new ArgumentException("Server Execution Mode not supported!", (string?)_mockerOptions.ExecutionMode.ToString())
        };
    }
}