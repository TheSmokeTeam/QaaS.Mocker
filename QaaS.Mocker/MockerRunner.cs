using QaaS.Framework.Executions;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
public class MockerRunner : IRunner
{
    private readonly ExecutionBuilder? _executionBuilder;
    private readonly Action<int> _exitAction;
    private int? BootstrapHandledExitCode { get; set; }

    public MockerRunner(ExecutionBuilder? executionBuilder, Action<int>? exitAction = null)
    {
        _executionBuilder = executionBuilder;
        _exitAction = exitAction ?? Environment.Exit;
    }

    /// <summary>
    /// Gets the configured execution builder for custom runner implementations.
    /// </summary>
    protected ExecutionBuilder? ExecutionBuilder => _executionBuilder;

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

        if (_executionBuilder == null)
            return;

        ExitProcess(StartExecution(BuildExecution(_executionBuilder)));
    }

    /// <summary>
    /// Builds the mocker execution from the configured execution builder.
    /// </summary>
    protected virtual BaseExecution BuildExecution(ExecutionBuilder executionBuilder)
    {
        return executionBuilder.Build();
    }

    /// <summary>
    /// Starts the built execution and returns its exit code.
    /// </summary>
    protected virtual int StartExecution(BaseExecution execution)
    {
        return execution.Start();
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
