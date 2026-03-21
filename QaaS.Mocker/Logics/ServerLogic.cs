using QaaS.Framework.Executions.Logics;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Servers.Servers;

namespace QaaS.Mocker.Logics;

/// <summary>
/// Starts the server runtime as part of the execution pipeline.
/// </summary>
public class ServerLogic(IServer server) : ILogic
{
    /// <summary>
    /// Always enables server execution whenever a runtime has been built.
    /// </summary>
    public bool ShouldRun(ExecutionType executionType) => true;

    /// <summary>
    /// Starts the configured server and preserves the current execution data.
    /// </summary>
    public ExecutionData Run(ExecutionData executionData)
    {
        server.Start();
        return executionData;
    }
}
