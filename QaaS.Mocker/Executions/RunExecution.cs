using Autofac;
using Microsoft.Extensions.Logging;
using QaaS.Framework.SDK.ContextObjects;
using QaaS.Mocker.Stubs;

namespace QaaS.Mocker.Executions;

/// <summary>
/// Lint execution mode.
/// Run a Server according to the given configurations.
/// </summary>
public class RunExecution(Context context, bool runLocally) : BaseExecution(context, true)
{ 
    private const int SleepIntervalMs = 100;
    /// <inheritdoc />
    protected override int Execute(ILifetimeScope scope)
    {
        // Resolve all components
        var dataSourcesFacade = scope.Resolve<DataSources.Facade>();
        var transactionStubsFacade = scope.Resolve<StubFactory>();
        var serverFactory = scope.Resolve<Servers.ServerFactory>();
        var controllerFactory = scope.Resolve<Controller.ControllerFactory>();
        
        var dataSourceList = dataSourcesFacade.Run();
        var transactionStubList = transactionStubsFacade.Build(dataSourceList);
        var server = serverFactory.Build(dataSourceList, transactionStubList);
        var controller = controllerFactory.Build(server.State);

        var cts = new CancellationTokenSource();
        var serverTasks = new List<Task> { Task.Run(() => server.Start(), cts.Token) };
        if (controller != null) serverTasks.Add(Task.Run(() => controller.Start(), cts.Token));

        if (runLocally)
        {
            Thread.Sleep(SleepIntervalMs);
            Context.Logger.LogInformation("Press any key to exit...");
            Console.ReadKey();
            cts.Cancel();
            cts.Dispose();
            return 0;
        }

        Task.WhenAll(serverTasks).Wait(cts.Token);
        return 0;
    }
}