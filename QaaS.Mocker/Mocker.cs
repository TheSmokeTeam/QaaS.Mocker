using QaaS.Framework.Executions;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
public class Mocker(ExecutionBuilder? executionBuilder) : IRunner
{
    public void Run()
    {
        if (executionBuilder == null)
            return;

        Environment.Exit(executionBuilder.Build().Start());
    }
}
