using NUnit.Framework;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class InitializationTests
{
    [Test]
    public void Initialize_WithInvalidArgs_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => Initialization.Initialize(["--invalid-option"]));
    }

    [Test]
    public void Mocker_WithNullExecutionBuilder_DoesNotThrow()
    {
        var mocker = new Mocker(null);

        Assert.DoesNotThrow(() => mocker.Run());
    }
}
