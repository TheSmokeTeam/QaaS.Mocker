using QaaS.Framework.Executions;
using NUnit.Framework;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class MockerRunnerTests
{
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
        var runner = new MockerRunner(executionBuilder, code => exitCode = code);

        runner.Run();

        Assert.That(exitCode, Is.EqualTo(7));
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
}
