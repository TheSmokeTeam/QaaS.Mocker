using QaaS.Framework.Executions;
using System.Diagnostics;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
public class MockerRunner : IRunner
{
    private readonly Action<int> _exitAction;
    internal Action? DrainCliLogger { get; set; }
    private int? BootstrapHandledExitCode { get; set; }

    public MockerRunner(IEnumerable<ExecutionBuilder>? executionBuilders, Action<int>? exitAction = null)
    {
        ExecutionBuilders = executionBuilders?.ToList() ?? [];
        _exitAction = exitAction ?? Environment.Exit;
    }

    /// <summary>
    /// Gets the configured execution builders for custom runner implementations.
    /// </summary>
    public List<ExecutionBuilder> ExecutionBuilders { get; }

    /// <summary>
    /// Gets the configured execution builder when only one execution builder is present.
    /// </summary>
    protected ExecutionBuilder? ExecutionBuilder => ExecutionBuilders.Count switch
    {
        0 => null,
        1 => ExecutionBuilders[0],
        _ => throw new InvalidOperationException(
            "Multiple execution builders are configured. Use ExecutionBuilders instead of ExecutionBuilder.")
    };

    /// <summary>
    /// Runs the configured QaaS.Mocker execution.
    /// </summary>
    /// <remarks>
    /// Call this after the mocker execution has been configured and resolved. The method delegates to the underlying execution host.
    /// </remarks>
    /// <qaas-docs group="Runtime" subgroup="Mocker Runner" />
    public virtual void Run()
    {
        if (BootstrapHandledExitCode.HasValue)
        {
            SetProcessExitCode(BootstrapHandledExitCode.Value);
            return;
        }

        if (ExecutionBuilders.Count == 0)
            return;

        var executions = BuildExecutions();
        int exitCode;
        try
        {
            exitCode = StartExecutions(executions);
        }
        finally
        {
            if (DrainCliLogger != null && executions.Any(execution => execution is Execution { LoggedExecution: true }))
                DrainCliLogger();
            DrainCliLogger = null;
        }

        ExitProcess(exitCode);
    }

    internal static void DrainLogger(IDisposable logger, long started, bool sinkConnected)
    {
        // An established sink needs time to serialize and post the last batch. Unreachable sinks can retry for seconds.
        var budget = sinkConnected ? TimeSpan.FromSeconds(2) :
            TimeSpan.FromTicks(Math.Min(Stopwatch.GetElapsedTime(started).Ticks / 100, 40 * TimeSpan.TicksPerMillisecond));
        // A connected sink can wait for bulk I/O. Leave pool threads available for its asynchronous continuations.
        var scheduling = sinkConnected ? TaskCreationOptions.LongRunning : TaskCreationOptions.None;
        var drain = Task.Factory.StartNew(() =>
        {
            try { logger.Dispose(); }
            catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
            {
                // Keep the original execution outcome if a logging provider fails during shutdown.
            }
        }, CancellationToken.None, scheduling | TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        drain.Wait(budget);
    }

    /// <summary>
    /// Builds the mocker execution from the configured execution builder.
    /// </summary>
    protected virtual BaseExecution BuildExecution(ExecutionBuilder executionBuilder)
    {
        return executionBuilder.Build();
    }

    /// <summary>
    /// Builds the configured mocker executions from the configured execution builders.
    /// </summary>
    protected virtual List<BaseExecution> BuildExecutions()
    {
        return ExecutionBuilders.Select(BuildExecution).ToList();
    }

    /// <summary>
    /// Starts the built execution and returns its exit code.
    /// </summary>
    protected virtual int StartExecution(BaseExecution execution)
    {
        return execution.Start();
    }

    /// <summary>
    /// Starts the built executions and returns the aggregated exit code.
    /// </summary>
    /// <remarks>
    /// Executions are started concurrently so long-lived runtimes do not block later builders from
    /// entering their own start sequence.
    /// </remarks>
    protected virtual int StartExecutions(IReadOnlyCollection<BaseExecution> executions)
    {
        var executionTasks = executions
            .Select(execution => Task.Run(() => StartExecution(execution)))
            .ToArray();

        return Task.WhenAll(executionTasks)
            .GetAwaiter()
            .GetResult()
            .Sum();
    }

    /// <summary>
    /// Completes the run by invoking the configured exit handler.
    /// </summary>
    protected virtual void ExitProcess(int exitCode)
    {
        _exitAction(exitCode);
    }

    /// <summary>
    /// Completes a bootstrap-handled run without entering the normal execution pipeline.
    /// </summary>
    protected virtual void SetProcessExitCode(int exitCode)
    {
        Environment.ExitCode = exitCode;
    }

    /// <summary>
    /// Marks the runner as a bootstrap-handled result so help, version, and parse failures do not
    /// continue into the execution pipeline.
    /// </summary>
    internal MockerRunner WithBootstrapHandledExitCode(int exitCode)
    {
        BootstrapHandledExitCode = exitCode;
        return this;
    }
}
