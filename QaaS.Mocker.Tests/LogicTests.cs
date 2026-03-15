using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.ExecutionObjects;
using QaaS.Mocker.Controller.Controllers;
using QaaS.Mocker.Logics;
using QaaS.Mocker.Servers.Servers;

namespace QaaS.Mocker.Tests;

[TestFixture]
public class LogicTests
{
    [Test]
    public void ControllerLogic_ShouldAlwaysRun_AndRunStartsController()
    {
        var controller = new Mock<IController>();
        var executionData = new ExecutionData();
        var logic = new ControllerLogic(controller.Object);

        var shouldRun = logic.ShouldRun(default);
        var result = logic.Run(executionData);

        Assert.Multiple(() =>
        {
            Assert.That(shouldRun, Is.True);
            Assert.That(result, Is.SameAs(executionData));
        });
        controller.Verify(instance => instance.Start(), Times.Once);
    }

    [Test]
    public void ServerLogic_ShouldAlwaysRun_AndRunStartsServer()
    {
        var server = new Mock<IServer>();
        var executionData = new ExecutionData();
        var logic = new ServerLogic(server.Object);

        var shouldRun = logic.ShouldRun(default);
        var result = logic.Run(executionData);

        Assert.Multiple(() =>
        {
            Assert.That(shouldRun, Is.True);
            Assert.That(result, Is.SameAs(executionData));
        });
        server.Verify(instance => instance.Start(), Times.Once);
    }
}
