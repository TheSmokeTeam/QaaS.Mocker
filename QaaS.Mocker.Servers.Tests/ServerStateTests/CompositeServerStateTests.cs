using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.Session;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.Exceptions;
using QaaS.Mocker.Servers.ServerStates;
using NUnit.Framework;

namespace QaaS.Mocker.Servers.Tests.ServerStateTests;

[TestFixture]
public class CompositeServerStateTests
{
    [Test]
    public void Constructor_WithInputAndOutputStates_AggregatesBothInputOutputState()
    {
        var state = new CompositeServerState(
        [
            new FakeServerState(InputOutputState.OnlyInput, "HttpAction"),
            new FakeServerState(InputOutputState.OnlyOutput, "SocketAction")
        ]);

        Assert.That(state.InputOutputState, Is.EqualTo(InputOutputState.BothInputOutput));
    }

    [Test]
    public void ChangeActionStub_WithMatchingAction_RoutesToOwningServer()
    {
        var httpState = new FakeServerState(InputOutputState.BothInputOutput, "HealthAction");
        var grpcState = new FakeServerState(InputOutputState.BothInputOutput, "EchoAction");
        var compositeState = new CompositeServerState([httpState, grpcState]);

        compositeState.ChangeActionStub("EchoAction", "GrpcStub");

        Assert.Multiple(() =>
        {
            Assert.That(httpState.ChangedActions, Is.Empty);
            Assert.That(grpcState.ChangedActions,
                Is.EqualTo(new[] { ("EchoAction", "GrpcStub") }));
        });
    }

    [Test]
    public void TriggerAction_WithDuplicateActionAcrossServers_ThrowsInvalidOperationException()
    {
        var compositeState = new CompositeServerState(
        [
            new FakeServerState(InputOutputState.OnlyInput, "SharedAction"),
            new FakeServerState(InputOutputState.OnlyOutput, "SharedAction")
        ]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            compositeState.TriggerAction("SharedAction", 100));

        Assert.That(exception!.Message, Does.Contain("multiple servers"));
    }

    [Test]
    public void GetCache_WithMultipleCaches_RetrievesInputAndOutputAcrossServers()
    {
        var inputCache = new FakeCache(["input-a"], []);
        var outputCache = new FakeCache([], ["output-b"]);
        var compositeState = new CompositeServerState(
        [
            new FakeServerState(InputOutputState.OnlyInput, "InputAction", inputCache),
            new FakeServerState(InputOutputState.OnlyOutput, "OutputAction", outputCache)
        ]);

        var cache = compositeState.GetCache();

        Assert.Multiple(() =>
        {
            Assert.That(cache.RetrieveFirstOrDefaultStringInput(), Is.EqualTo("input-a"));
            Assert.That(cache.RetrieveFirstOrDefaultStringOutput(), Is.EqualTo("output-b"));
        });
    }

    private sealed class FakeServerState : IServerState
    {
        private readonly HashSet<string> _actions;
        private readonly ICache _cache;

        public FakeServerState(InputOutputState inputOutputState, string actionName, ICache? cache = null)
        {
            InputOutputState = inputOutputState;
            _actions = [actionName];
            _cache = cache ?? new FakeCache([], []);
        }

        public InputOutputState InputOutputState { get; init; }

        public List<(string ActionName, string StubName)> ChangedActions { get; } = [];

        public bool HasAction(string actionName)
        {
            return _actions.Contains(actionName);
        }

        public void ChangeActionStub(string actionName, string stubName)
        {
            if (!HasAction(actionName))
                throw new ActionDoesNotExistException($"Cannot change action '{actionName}' that doesn't exist");

            ChangedActions.Add((actionName, stubName));
        }

        public void TriggerAction(string actionName, int? timeoutMs)
        {
            if (!HasAction(actionName))
                throw new ActionDoesNotExistException($"Cannot trigger action '{actionName}' that doesn't exist");
        }

        public ICache GetCache() => _cache;
    }

    private sealed class FakeCache(IEnumerable<string> inputs, IEnumerable<string> outputs) : ICache
    {
        private readonly Queue<string> _inputs = new(inputs);
        private readonly Queue<string> _outputs = new(outputs);

        public bool EnableStorage { get; set; }
        public string? CachedAction { get; set; }
        public DataFilter InputDataFilter { get; set; } = new();
        public DataFilter OutputDataFilter { get; set; } = new();

        public string? RetrieveFirstOrDefaultStringInput()
        {
            return _inputs.Count == 0 ? null : _inputs.Dequeue();
        }

        public string? RetrieveFirstOrDefaultStringOutput()
        {
            return _outputs.Count == 0 ? null : _outputs.Dequeue();
        }
    }
}
