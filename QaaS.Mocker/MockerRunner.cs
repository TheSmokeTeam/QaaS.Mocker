using QaaS.Framework.Executions;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
public class MockerRunner(ExecutionBuilder? executionBuilder, Action<int>? exitAction = null) : IRunner
{
    private readonly Action<int> _exitAction = exitAction ?? Environment.Exit;

    public void Run()
    {
        if (executionBuilder == null)
            return;

        _exitAction(executionBuilder.Build().Start());
    }
}
