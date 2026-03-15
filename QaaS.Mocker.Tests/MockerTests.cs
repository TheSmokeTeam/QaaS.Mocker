using NUnit.Framework;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class MockerTests
{
    [Test]
    public void Run_WithNullExecutionBuilder_ReturnsWithoutThrowing()
    {
        var runner = new Mocker(null);

        Assert.DoesNotThrow(() => runner.Run());
    }
}
