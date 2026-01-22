using QaaS.Framework.Executions.Logics;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Controller.Controllers;

namespace QaaS.Mocker.Logics;

public class ControllerLogic(IController controller) : ILogic
{
    public bool ShouldRun(ExecutionType executionType) => true;

    public ExecutionData Run(ExecutionData executionData)
    {
        controller.Start();
        return executionData;
    }
}