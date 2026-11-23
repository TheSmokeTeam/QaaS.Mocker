using QaaS.Framework.Executions;
using QaaS.Framework.SDK.ContextObjects;
using Microsoft.Extensions.Logging;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Options;
using QaaS.Mocker.Servers;

namespace QaaS.Mocker;

/// <summary>
/// Represents a single execution of QaaS.Mocker.
/// </summary>
public class Execution : BaseExecution
{
    private readonly IExecutionConsole _executionConsole;
    private readonly ExecutionMode _executionMode;
    private readonly bool _runLocally;

    internal MockerLogIdentity? LogIdentity { get; init; }
    internal bool LoggedExecution { get; private set; }
    internal ServerLogic ServerLogic { get; init; } = null!;
    internal ControllerLogic? ControllerLogic { get; init; }
    internal TemplateLogic TemplateLogic { get; init; } = null!;

    /// <summary>
    /// Initializes a runtime execution that uses the real console for local-run interactions.
    /// </summary>
    public Execution(
        ExecutionMode executionMode,
        Context context,
        bool runLocally)
        : this(executionMode, context, runLocally, SystemExecutionConsole.Instance)
    {
    }

    internal Execution(
        ExecutionMode executionMode,
        Context context,
        bool runLocally,
        IExecutionConsole executionConsole)
    {
        _executionMode = executionMode;
        _runLocally = runLocally;
        _executionConsole = executionConsole;
        Context = context;
    }

    /// <summary>
    /// Starts the execution flow that matches the configured mode.
    /// </summary>
    public override int Start()
    {
        LoggedExecution = false;
        Context.Logger.LogInformation(
            "Starting QaaS.Mocker in {ExecutionMode} mode (RunLocally: {RunLocally})",
            _executionMode, _runLocally);

        if (_executionMode == ExecutionMode.Template)
            return Template();
        if (_executionMode != ExecutionMode.Run)
            throw new ArgumentOutOfRangeException(nameof(_executionMode), _executionMode, "Unsupported mocker execution mode");

        var identity = LogIdentity ?? MockerLogIdentity.From(Context);
        if (identity == null)
            return Run();

        LogExecution(identity, "MockerExecutionStarted", null);
        int result;
        try
        {
            result = Run();
        }
        catch
        {
            LogExecution(identity, "MockerExecutionFinished", 1);
            throw;
        }
        LogExecution(identity, "MockerExecutionFinished", result == 0 ? 0 : 1);
        return result;
    }

    private void LogExecution(MockerLogIdentity identity, string eventName, int? failed)
    {
        try
        {
            Context.Logger.LogDebug(
                "{QaasEvent} (ExecutionId: {ExecutionId}, Team: {Team}, System: {System}, Failed: {Failed})",
                eventName, identity.ExecutionId, identity.Team, identity.System, failed);
            LoggedExecution = true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // Observability must preserve the mocker's original result and exceptions.
        }
    }

    private int Template()
    {
        Context.Logger.LogInformation("Generating template output");
        TemplateLogic.Run(Context.ExecutionData);
        return 0;
    }

    private int Run()
    {
        var runTasks = new List<Task>
        {
            StartLongRunningTask(() => ServerLogic.Run(Context.ExecutionData))
        };
        Context.Logger.LogInformation("Started server runtime task");

        if (ControllerLogic != null)
        {
            runTasks.Add(StartLongRunningTask(() => ControllerLogic.Run(Context.ExecutionData)));
            Context.Logger.LogInformation("Started controller runtime task");
        }
        else
        {
            Context.Logger.LogInformation("Controller runtime is disabled for this execution");
        }

        if (_runLocally)
        {
            if (!_executionConsole.IsInputRedirected)
            {
                Context.Logger.LogInformation("Press any key to stop the mocker...");
                _executionConsole.ReadKey(intercept: true);
            }
            else
            {
                Context.Logger.LogInformation("Console input is redirected; stopping run-locally execution immediately.");
            }

            return 0;
        }

        Task.WaitAll(runTasks.ToArray());
        Context.Logger.LogInformation("Runtime tasks completed");
        return 0;
    }

    private static Task StartLongRunningTask(Action action)
    {
        // Listener runtimes are intentionally long-lived, so schedule them outside the normal
        // thread-pool heuristics to avoid starving short-lived work.
        return Task.Factory.StartNew(action,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public override void Dispose()
    {
    }
}

/// <summary>
/// Abstracts console access so run-locally behavior can be tested deterministically.
/// </summary>
internal interface IExecutionConsole
{
    bool IsInputRedirected { get; }
    ConsoleKeyInfo ReadKey(bool intercept);
}

/// <summary>
/// Production implementation of <see cref="IExecutionConsole"/> backed by <see cref="Console"/>.
/// </summary>
internal sealed class SystemExecutionConsole : IExecutionConsole
{
    public static SystemExecutionConsole Instance { get; } = new();

    private SystemExecutionConsole()
    {
    }

    public bool IsInputRedirected => Console.IsInputRedirected;

    public ConsoleKeyInfo ReadKey(bool intercept) => Console.ReadKey(intercept);
}
