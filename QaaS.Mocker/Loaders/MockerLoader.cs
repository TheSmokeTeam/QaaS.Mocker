using Autofac;
using Microsoft.Extensions.Configuration;
using QaaS.Framework.Executions.Loaders;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Options;

namespace QaaS.Mocker.Loaders;

public class MockerLoader : BaseLoader<MockerOptions, Mocker>
{
    private readonly ILifetimeScope _runScope;

    public MockerLoader(MockerOptions options, string? executionId = null) : base(options, executionId)
    {
        _runScope = InitializeScope();
    }

    private InternalContext GetLoadedContext()
    {
        // referenceResolutionPaths & uniqueIdPathRegexes
        var contextBuilder = new ContextBuilder(_runScope.Resolve<IConfigurationBuilder>());

        contextBuilder.SetLogger(Logger);
        contextBuilder.SetConfigurationFile(Options.ConfigurationFile!);
        foreach (var overwriteFile in Options.OverwriteFiles)
            contextBuilder.WithOverwriteFile(overwriteFile);
        foreach (var overwriteArgument in Options.OverwriteArguments)
            contextBuilder.WithOverwriteArgument(overwriteArgument);
        if (!Options.DontResolveWithEnvironmentVariables)
            contextBuilder.WithEnvironmentVariableResolution();
        return contextBuilder.BuildInternal();
    }

    private ExecutionBuilder LoadContextToExecutionBuilder(InternalContext context)
    {
        var runBuilder = new ExecutionBuilder(context, Options.ExecutionMode!.Value, Options.RunLocally);
        return runBuilder;
    }

    private ILifetimeScope InitializeScope()
    {
        return new ContainerBuilder().Build().BeginLifetimeScope(scope =>
        {
            // Must not be single instance so it builds a new configuration builder for every context
            scope.RegisterType<ConfigurationBuilder>().As<IConfigurationBuilder>();
        });
    }

    public override Mocker GetLoadedRunner() => new(LoadContextToExecutionBuilder(GetLoadedContext()));
}