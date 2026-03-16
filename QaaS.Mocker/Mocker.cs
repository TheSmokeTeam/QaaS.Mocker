using QaaS.Framework.Executions;
using System.Diagnostics.CodeAnalysis;

namespace QaaS.Mocker;

/// <summary>
/// Runner object representing a single QaaS.Mocker run.
/// </summary>
[ExcludeFromCodeCoverage]
public class Mocker(ExecutionBuilder? executionBuilder) : IRunner
{
    public void Run()
    {
        if (executionBuilder == null)
            return;

        Environment.Exit(executionBuilder.Build().Start());
    }
}
