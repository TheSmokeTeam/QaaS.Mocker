using QaaS.Framework.Executions;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
public class MockerRunner(ExecutionBuilder? executionBuilder, Action<int>? exitAction = null) : IRunner
{
    private readonly Action<int> _exitAction = exitAction ?? Environment.Exit;
    private int? BootstrapHandledExitCode { get; set; }

    /// <summary>
    /// Runs the configured QaaS.Mocker execution.
    /// </summary>
    /// <remarks>
    /// Call this after the mocker execution has been configured and resolved. The method delegates to the underlying execution host.
    /// </remarks>
    /// <qaas-docs group="Runtime" subgroup="Mocker Runner" />
    public void Run()
    {
        if (BootstrapHandledExitCode.HasValue)
        {
            Environment.ExitCode = BootstrapHandledExitCode.Value;
            return;
        }

        if (executionBuilder == null)
            return;

        _exitAction(executionBuilder.Build().Start());
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
