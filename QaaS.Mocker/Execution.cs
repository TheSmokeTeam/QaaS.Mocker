using QaaS.Framework.Executions;
using QaaS.Framework.SDK.ContextObjects;
using Microsoft.Extensions.Logging;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Options;

namespace QaaS.Mocker;

/// <summary>
/// Represents a single execution of QaaS.Mocker.
/// </summary>
public class Execution : BaseExecution
{
    private readonly ExecutionMode _executionMode;
    private readonly bool _runLocally;

    internal ServerLogic ServerLogic { get; init; } = null!;
    internal ControllerLogic? ControllerLogic { get; init; }
    internal TemplateLogic TemplateLogic { get; init; } = null!;

    public Execution(ExecutionMode executionMode, Context context, bool runLocally)
    {
        _executionMode = executionMode;
        _runLocally = runLocally;
        Context = context;
    }

    public override int Start()
    {
        Context.Logger.LogInformation("Running Mocker in {ExecutionMode} mode", _executionMode);

        return _executionMode switch
        {
            ExecutionMode.Run => Run(),
            ExecutionMode.Lint => Lint(),
            ExecutionMode.Template => Template(),
            _ => throw new ArgumentOutOfRangeException(nameof(_executionMode), _executionMode,
                "Unsupported mocker execution mode")
        };
    }

    private int Lint() => 0;

    private int Template()
    {
        TemplateLogic.Run(Context.ExecutionData);
        return 0;
    }

    private int Run()
    {
        var runTasks = new List<Task>
        {
            Task.Run(() => ServerLogic.Run(Context.ExecutionData))
        };

        if (ControllerLogic != null)
            runTasks.Add(Task.Run(() => ControllerLogic.Run(Context.ExecutionData)));

        if (_runLocally)
        {
            if (!Console.IsInputRedirected)
            {
                Context.Logger.LogInformation("Press any key to stop the mocker...");
                Console.ReadKey(intercept: true);
            }
            else
            {
                Context.Logger.LogInformation("Console input is redirected; stopping run-locally execution immediately.");
            }

            return 0;
        }

        Task.WaitAll(runTasks.ToArray());
        return 0;
    }

    public override void Dispose()
    {
    }
}
