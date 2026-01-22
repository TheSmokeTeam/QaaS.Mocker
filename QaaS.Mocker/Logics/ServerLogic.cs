using QaaS.Framework.Executions.Logics;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Servers.Servers;

namespace QaaS.Mocker.Logics;

public class ServerLogic(IServer server) : ILogic
{
    public bool ShouldRun(ExecutionType executionType) => true;

    public ExecutionData Run(ExecutionData executionData)
    {
        server.Start();
        return executionData;
    }
}