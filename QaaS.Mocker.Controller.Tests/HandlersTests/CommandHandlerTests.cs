using Moq;
using NUnit.Framework;
using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.MockerObjects.ConfigurationObjects.Command;
using QaaS.Framework.SDK.Session;
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

    [Test]
    public void HandleRequest_WithConsumeAndBothInputOutput_PushesInputAndOutputQueues()
    {
        var cache = new TestCache
        {
            InputValues = ["input-a"],
            OutputValues = ["output-a"]
        };

        var pushed = new List<(string Queue, string Value)>();
        var allMessagesPushed = new ManualResetEventSlim(false);
        var (handler, _, database, _) = CreateHandler(cache, InputOutputState.BothInputOutput, serverName: "SERVER-A");
        database
            .Setup(db => db.ListRightPush(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, When, CommandFlags>((queue, value, _, _) =>
            {
                lock (pushed)
                {
                    pushed.Add((queue.ToString(), value.ToString()));
                    if (pushed.Count >= 2)
                        allMessagesPushed.Set();
                }
            })
            .Returns(1);

        var response = handler.Invoke(new CommandRequest
        {
            Id = "consume-1",
            Command = CommandType.Consume,
            Consume = new Consume { TimeoutMs = 120 }
        });

        var didPushAll = allMessagesPushed.Wait(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Succeeded));
            Assert.That(didPushAll, Is.True);
            Assert.That(pushed, Does.Contain(("server-a:input", "input-a")));
            Assert.That(pushed, Does.Contain(("server-a:output", "output-a")));
        });
    }

    [Test]
    public void HandleRequest_WithConsumeAndOnlyInput_PushesOnlyInputQueue()
    {
        var cache = new TestCache
        {
            InputValues = ["input-only"],
            OutputValues = ["output-should-not-be-used"]
        };

        var pushed = new List<(string Queue, string Value)>();
        var inputPushed = new ManualResetEventSlim(false);
        var (handler, _, database, _) = CreateHandler(cache, InputOutputState.OnlyInput, serverName: "server-b");
        database
            .Setup(db => db.ListRightPush(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, When, CommandFlags>((queue, value, _, _) =>
            {
                lock (pushed)
                {
                    pushed.Add((queue.ToString(), value.ToString()));
                    inputPushed.Set();
                }
            })
            .Returns(1);

        var response = handler.Invoke(new CommandRequest
        {
            Id = "consume-2",
            Command = CommandType.Consume,
            Consume = new Consume { TimeoutMs = 120 }
        });

        var didPushInput = inputPushed.Wait(TimeSpan.FromSeconds(2));

        Assert.Multiple(() =>
        {
            Assert.That(response, Is.Not.Null);
            Assert.That(response!.Status, Is.EqualTo(Status.Succeeded));
            Assert.That(didPushInput, Is.True);
            Assert.That(pushed, Does.Contain(("server-b:input", "input-only")));
            Assert.That(pushed.Any(item => item.Queue == "server-b:output"), Is.False);
        });
    }

    private static (TestableCommandHandler Handler, Mock<IServerState> ServerState, Mock<IDatabase> Database,
        Mock<ISubscriber> Subscriber) CreateHandler(
        ICache? cache = null,
        InputOutputState inputOutputState = InputOutputState.BothInputOutput,
        string serverName = "server-a")
    {
        cache ??= new TestCache();

        var serverState = new Mock<IServerState>();
        serverState.SetupGet(state => state.InputOutputState).Returns(inputOutputState);
        serverState.Setup(state => state.GetCache()).Returns(cache);

        var database = new Mock<IDatabase>();
        var subscriber = new Mock<ISubscriber>();

        var handler = new TestableCommandHandler(
            serverState.Object,
            database.Object,
            subscriber.Object,
            serverName,
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

    private sealed class TestCache : ICache
    {
        private readonly Queue<string> _input = new();
        private readonly Queue<string> _output = new();
        private readonly object _lock = new();

        public bool EnableStorage { get; set; }
        public string? CachedAction { get; set; }
        public DataFilter InputDataFilter { get; set; } = new();
        public DataFilter OutputDataFilter { get; set; } = new();

        public string[] InputValues
        {
            init
            {
                foreach (var item in value)
                    _input.Enqueue(item);
            }
        }

        public string[] OutputValues
        {
            init
            {
                foreach (var item in value)
                    _output.Enqueue(item);
            }
        }

        public string? RetrieveFirstOrDefaultStringInput()
        {
            lock (_lock)
            {
                return _input.TryDequeue(out var value) ? value : null;
            }
        }

        public string? RetrieveFirstOrDefaultStringOutput()
        {
            lock (_lock)
            {
                return _output.TryDequeue(out var value) ? value : null;
            }
        }
    }
}
