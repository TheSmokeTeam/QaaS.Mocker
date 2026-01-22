using QaaS.Framework.Executions;

namespace QaaS.Mocker;

public class Mocker(ExecutionBuilder executionBuilder) : IRunner
{

    public void Run()
    {
        Environment.Exit(executionBuilder.Build().Start());
    }
}