using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Command;
using QaaS.Mocker.Controller.Handlers;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ServerStates;
using StackExchange.Redis;

namespace QaaS.Mocker.Controller.Tests.HandlersTests;

[TestFixture]
public class CommandHandlerTests
{
    [Test]
    public void HandleRequest_WithMissingConsumePayload_ReturnsFailed()
    {
        var (handler, serverState, _, _) = CreateHandler();

        var response = handler.Invoke(new CommandRequest
        {
            Id = "req-1",
            Command = CommandType.Consume
        });

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Failed));
            Assert.That(response.ExceptionMessage, Does.Contain("Consume payload is required"));
        });
        serverState.Verify(state => state.ChangeActionStub(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        serverState.Verify(state => state.TriggerAction(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public void HandleRequest_WithMissingTriggerActionName_ReturnsFailed()
    {
        var (handler, serverState, _, _) = CreateHandler();

        var response = handler.Invoke(new CommandRequest
        {
            Id = "req-2",
            Command = CommandType.TriggerAction,
            TriggerAction = new TriggerAction { ActionName = null, TimeoutMs = 100 }
        });

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Failed));
            Assert.That(response.ExceptionMessage, Does.Contain("TriggerAction.ActionName is required"));
        });
        serverState.Verify(state => state.TriggerAction(It.IsAny<string>(), It.IsAny<int?>()), Times.Never);
    }

    [Test]
    public void HandleRequest_WithValidChangeActionStub_CallsServerStateAndReturnsSucceeded()
    {
        var (handler, serverState, _, _) = CreateHandler();

        var response = handler.Invoke(new CommandRequest
        {
            Id = "req-3",
            Command = CommandType.ChangeActionStub,
            ChangeActionStub = new ChangeActionStub
            {
                ActionName = "HealthAction",
                StubName = "HealthStub"
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Succeeded));
            Assert.That(response.ExceptionMessage, Is.Null);
            Assert.That(response.Command, Is.EqualTo(CommandType.ChangeActionStub));
        });
        serverState.Verify(state => state.ChangeActionStub("HealthAction", "HealthStub"), Times.Once);
    }

    [Test]
    public void HandleRequest_WithValidTriggerAction_CallsServerStateAndReturnsSucceeded()
    {
        var (handler, serverState, _, _) = CreateHandler();

        var response = handler.Invoke(new CommandRequest
        {
            Id = "req-4",
            Command = CommandType.TriggerAction,
            TriggerAction = new TriggerAction { ActionName = "HealthAction", TimeoutMs = 150 }
        });

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Succeeded));
            Assert.That(response.ExceptionMessage, Is.Null);
            Assert.That(response.Command, Is.EqualTo(CommandType.TriggerAction));
        });
        serverState.Verify(state => state.TriggerAction("HealthAction", 150), Times.Once);
    }

    private static (TestableCommandHandler Handler, Mock<IServerState> ServerState, Mock<IDatabase> Database,
        Mock<ISubscriber> Subscriber) CreateHandler()
    {
        var cache = new Mock<ICache>();
        cache.SetupAllProperties();
        cache.Setup(c => c.RetrieveFirstOrDefaultStringInput()).Returns((string?)null);
        cache.Setup(c => c.RetrieveFirstOrDefaultStringOutput()).Returns((string?)null);

        var serverState = new Mock<IServerState>();
        serverState.SetupGet(state => state.InputOutputState).Returns(InputOutputState.BothInputOutput);
        serverState.Setup(state => state.GetCache()).Returns(cache.Object);

        var database = new Mock<IDatabase>();
        var subscriber = new Mock<ISubscriber>();

        var handler = new TestableCommandHandler(
            serverState.Object,
            database.Object,
            subscriber.Object,
            "server-a",
            "instance-1",
            Globals.Logger);

        return (handler, serverState, database, subscriber);
    }

    private sealed class TestableCommandHandler(
        IServerState serverState,
        IDatabase databaseClient,
        ISubscriber subscriberClient,
        string serverName,
        string serverInstanceId,
        Microsoft.Extensions.Logging.ILogger logger)
        : CommandHandler(serverState, databaseClient, subscriberClient, serverName, serverInstanceId, logger)
    {
        public CommandResponse? Invoke(CommandRequest request) => HandleRequest("runner:mocker:commands", request);
    }
}
