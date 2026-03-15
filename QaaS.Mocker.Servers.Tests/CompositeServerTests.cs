using QaaS.Framework.SDK.ConfigurationObjects;
using QaaS.Framework.SDK.Session;
using QaaS.Mocker.Servers.Caches;
using QaaS.Mocker.Servers.ServerStates;
using QaaS.Mocker.Servers.Servers;
using NUnit.Framework;

namespace QaaS.Mocker.Servers.Tests;

[TestFixture]
public class CompositeServerTests
{
    [Test]
    public void Constructor_WithNoServers_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new CompositeServer([], Globals.Logger));
    }

    [Test]
    public void Start_WhenServerExitsNormally_ThrowsUnexpectedExitException()
    {
        var server = new FakeServer(() => { });
        var composite = new CompositeServer([server], Globals.Logger);

        var exception = Assert.Throws<InvalidOperationException>(() => composite.Start());

        Assert.That(exception!.Message, Does.Contain("exited unexpectedly"));
    }

    [Test]
    public void Start_WhenServerFaults_RethrowsInnerException()
    {
        var composite = new CompositeServer(
        [
            new FakeServer(() => throw new InvalidOperationException("boom"))
        ], Globals.Logger);

        var exception = Assert.Throws<InvalidOperationException>(() => composite.Start());

        Assert.That(exception!.Message, Is.EqualTo("boom"));
    }

    [Test]
    public void Start_WhenServerThrowsEmptyAggregateException_RethrowsFlattenedAggregate()
    {
        var composite = new CompositeServer(
        [
            new FakeServer(() => throw new AggregateException())
        ], Globals.Logger);

        var exception = Assert.Throws<AggregateException>(() => composite.Start());

        Assert.That(exception!.InnerExceptions, Is.Empty);
    }

    private sealed class FakeServer(Action startAction) : IServer
    {
        public IServerState State { get; init; } = new FakeServerState();

        public void Start() => startAction();
    }

    private sealed class FakeServerState : IServerState
    {
        public InputOutputState InputOutputState { get; init; } = InputOutputState.NoInputOutput;
        public bool HasAction(string actionName) => false;
        public void ChangeActionStub(string actionName, string stubName) => throw new NotImplementedException();
        public void TriggerAction(string actionName, int? timeoutMs) => throw new NotImplementedException();
        public ICache GetCache() => new FakeCache();
    }

    private sealed class FakeCache : ICache
    {
        public bool EnableStorage { get; set; }
        public string? CachedAction { get; set; }
        public DataFilter InputDataFilter { get; set; } = new();
        public DataFilter OutputDataFilter { get; set; } = new();
        public string? RetrieveFirstOrDefaultStringInput() => null;
        public string? RetrieveFirstOrDefaultStringOutput() => null;
    }
}
