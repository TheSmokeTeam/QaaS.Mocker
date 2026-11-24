using System.Threading;
using System.Threading.Tasks;
using Moq;
using QaaS.Framework.Executions;
using NUnit.Framework;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class MockerRunnerTests
{
    [Test]
    public void Run_BuildFailureLeavesLoggerAvailableForRetry()
    {
        var failure = new InvalidOperationException("original build failure");
        var builder = new Mock<ExecutionBuilder>();
        builder.Setup(x => x.Build()).Throws(failure);
        var drained = false;
        var runner = new MockerRunner([builder.Object], _ => { }) { DrainCliLogger = () => drained = true };
        Assert.That(Assert.Throws<InvalidOperationException>(runner.Run), Is.SameAs(failure));
        Assert.That(drained, Is.False);
        Assert.That(runner.DrainCliLogger, Is.Not.Null);
    }

    [TestCase("0", 0)]
    [TestCase("275", 275)]
    [TestCase("10000", 2000)]
    [TestCase(null, 125)]
    [TestCase("invalid", 125)]
    [NonParallelizable]
    public void FlushTimeoutBoundsAcceptedValues(string? setting, int expected)
    {
        var previous = Environment.GetEnvironmentVariable("QAAS_USAGE_FLUSH_MS");
        try
        {
            Environment.SetEnvironmentVariable("QAAS_USAGE_FLUSH_MS", setting);
            Assert.That(MockerRunner.GetFlushBudget().TotalMilliseconds, Is.EqualTo(expected));
        }
        finally { Environment.SetEnvironmentVariable("QAAS_USAGE_FLUSH_MS", previous); }
    }

    [Test]
    public void DrainLogger_ConnectedSinkWaitsForSlowFinalBatch()
    {
        var delivered = new TaskCompletionSource();
        var logger = new Mock<IDisposable>();
        logger.Setup(x => x.Dispose()).Callback(() =>
        {
            // A busy but connected sink can need several hundred milliseconds to finish a write.
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
            delivered.SetResult();
        });

        MockerRunner.DrainLogger(logger.Object, TimeSpan.FromSeconds(2));

        Assert.That(delivered.Task.IsCompletedSuccessfully, Is.True);
    }

    [Test]
    public async Task DrainLogger_SlowOrThrowingProviderPreservesCaller()
    {
        using var release = new ManualResetEventSlim();
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var logger = new Mock<IDisposable>();
        logger.Setup(x => x.Dispose()).Callback(() =>
        {
            release.Wait();
            disposed.SetResult();
            throw new InvalidOperationException("sink failure");
        });
        try
        {
            await Task.Run(() => MockerRunner.DrainLogger(logger.Object, TimeSpan.FromMilliseconds(15))).WaitAsync(TimeSpan.FromSeconds(6));
            Assert.That(disposed.Task.IsCompleted, Is.False);
        }
        finally
        {
            release.Set();
        }
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Test]
    public void Run_WithNullExecutionBuilder_ReturnsWithoutThrowing()
    {
        var runner = new MockerRunner(null);

        Assert.DoesNotThrow(() => runner.Run());
    }

    [Test]
    public void Run_WithExecutionBuilder_UsesExecutionExitCode()
    {
        var executionBuilder = new StubExecutionBuilder(new StubExecution(7));
        var exitCode = -1;
        var runner = new MockerRunner([executionBuilder], code => exitCode = code);

        runner.Run();

        Assert.That(exitCode, Is.EqualTo(7));
    }

    [Test]
    public void Run_WithBootstrapHandledExitCode_SetsProcessExitCodeWithoutCallingExitAction()
    {
        var exitActionCalled = false;
        var runner = new MockerRunner(null, _ => exitActionCalled = true)
            .WithBootstrapHandledExitCode(3);

        runner.Run();

        Assert.Multiple(() =>
        {
            Assert.That(exitActionCalled, Is.False);
            Assert.That(Environment.ExitCode, Is.EqualTo(3));
        });

        Environment.ExitCode = 0;
    }

    [Test]
    public void Run_WithCustomRunner_UsesVirtualLifecycleHooks()
    {
        var executionBuilder = new StubExecutionBuilder(new StubExecution(11));
        var runner = new TrackingMockerRunner([executionBuilder]);

        runner.Run();

        Assert.Multiple(() =>
        {
            Assert.That(runner.BuildExecutionCalled, Is.True);
            Assert.That(runner.StartExecutionCalled, Is.True);
            Assert.That(runner.ExitProcessCalled, Is.True);
            Assert.That(runner.ObservedExitCode, Is.EqualTo(11));
        });
    }

    [Test]
    public void StartExecutions_WithBlockingExecutions_StartsAllExecutionsConcurrently()
    {
        using var allExecutionsStarted = new ManualResetEventSlim(false);
        using var releaseExecutions = new ManualResetEventSlim(false);
        var sharedState = new BlockingExecutionState();

        var firstExecution = new BlockingExecution(2, sharedState, allExecutionsStarted, releaseExecutions, 3);
        var secondExecution = new BlockingExecution(2, sharedState, allExecutionsStarted, releaseExecutions, 5);
        var runner = new ExposedStartExecutionsRunner();

        var runTask = Task.Run(() => runner.RunStartExecutions([firstExecution, secondExecution]));

        try
        {
            Assert.That(
                allExecutionsStarted.Wait(TimeSpan.FromSeconds(1)),
                Is.True,
                "Expected all executions to begin before any of them completed.");
        }
        finally
        {
            releaseExecutions.Set();
            _ = runTask.Wait(TimeSpan.FromSeconds(5));
        }

        Assert.That(runTask.Result, Is.EqualTo(8));
    }

    private sealed class StubExecutionBuilder(BaseExecution execution) : ExecutionBuilder
    {
        public override BaseExecution Build() => execution;
    }

    private sealed class StubExecution(int exitCode) : BaseExecution
    {
        public override int Start() => exitCode;

        public override void Dispose()
        {
        }
    }

    private sealed class TrackingMockerRunner(IEnumerable<ExecutionBuilder>? executionBuilders)
        : MockerRunner(executionBuilders)
    {
        public bool BuildExecutionCalled { get; private set; }
        public bool StartExecutionCalled { get; private set; }
        public bool ExitProcessCalled { get; private set; }
        public int? ObservedExitCode { get; private set; }

        protected override BaseExecution BuildExecution(ExecutionBuilder executionBuilder)
        {
            BuildExecutionCalled = true;
            return base.BuildExecution(executionBuilder);
        }

        protected override int StartExecution(BaseExecution execution)
        {
            StartExecutionCalled = true;
            return base.StartExecution(execution);
        }

        protected override void ExitProcess(int exitCode)
        {
            ExitProcessCalled = true;
            ObservedExitCode = exitCode;
        }
    }

    private sealed class ExposedStartExecutionsRunner() : MockerRunner(null, _ => { })
    {
        public int RunStartExecutions(IReadOnlyCollection<BaseExecution> executions)
        {
            return StartExecutions(executions);
        }
    }

    private sealed class BlockingExecution(
        int expectedExecutions,
        BlockingExecutionState sharedState,
        ManualResetEventSlim allExecutionsStarted,
        ManualResetEventSlim releaseExecutions,
        int exitCode) : BaseExecution
    {
        public override int Start()
        {
            if (Interlocked.Increment(ref sharedState.StartedExecutions) == expectedExecutions)
                allExecutionsStarted.Set();

            _ = releaseExecutions.Wait(TimeSpan.FromSeconds(5));
            return exitCode;
        }

        public override void Dispose()
        {
        }
    }

    private sealed class BlockingExecutionState
    {
        public int StartedExecutions;
    }
}
