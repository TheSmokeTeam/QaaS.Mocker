using QaaS.Framework.Executions.Logics;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Controller.Controllers;

namespace QaaS.Mocker.Logics;

/// <summary>
/// Starts the optional controller as part of the execution pipeline.
/// </summary>
public class ControllerLogic(IController controller) : ILogic
{
    /// <summary>
    /// Always enables controller execution whenever the runtime is built with a controller.
    /// </summary>
    public bool ShouldRun(ExecutionType executionType) => true;

    /// <summary>
    /// Starts the configured controller and preserves the current execution data.
    /// </summary>
    public ExecutionData Run(ExecutionData executionData)
    {
        controller.Start();
        return executionData;
    }
}
