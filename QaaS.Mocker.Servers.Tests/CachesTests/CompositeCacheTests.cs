using QaaS.Framework.SDK.Session;
using QaaS.Mocker.Servers.Caches;
using NUnit.Framework;

namespace QaaS.Mocker.Servers.Tests.CachesTests;

[TestFixture]
public class CompositeCacheTests
{
    [Test]
    public void EnableStorage_GetAndSet_PropagatesAcrossCaches()
    {
        var first = new FakeCache();
        var second = new FakeCache();
        var cache = new CompositeCache([first, second]);

        cache.EnableStorage = true;

        Assert.Multiple(() =>
        {
            Assert.That(cache.EnableStorage, Is.True);
            Assert.That(first.EnableStorage, Is.True);
            Assert.That(second.EnableStorage, Is.True);
        });
    }

    [Test]
    public void CachedAction_GetAndSet_PropagatesAcrossCaches()
    {
        var first = new FakeCache();
        var second = new FakeCache();
        var cache = new CompositeCache([first, second]);

        cache.CachedAction = "ActionA";

        Assert.Multiple(() =>
        {
            Assert.That(cache.CachedAction, Is.EqualTo("ActionA"));
            Assert.That(first.CachedAction, Is.EqualTo("ActionA"));
            Assert.That(second.CachedAction, Is.EqualTo("ActionA"));
        });
    }

    [Test]
    public void InputAndOutputFilters_GetAndSet_PropagatesAcrossCaches()
    {
        var first = new FakeCache();
        var second = new FakeCache();
        var cache = new CompositeCache([first, second]);
        var input = new DataFilter();
        var output = new DataFilter();

        cache.InputDataFilter = input;
        cache.OutputDataFilter = output;

        Assert.Multiple(() =>
        {
            Assert.That(cache.InputDataFilter, Is.SameAs(input));
            Assert.That(cache.OutputDataFilter, Is.SameAs(output));
            Assert.That(first.InputDataFilter, Is.SameAs(input));
            Assert.That(second.OutputDataFilter, Is.SameAs(output));
        });
    }

    [Test]
    public void RetrieveFirstOrDefaultStringInput_WhenFirstCacheIsEmpty_UsesNextCache()
    {
        var cache = new CompositeCache(
        [
            new FakeCache(),
            new FakeCache(inputs: ["payload-a"])
        ]);

        var payload = cache.RetrieveFirstOrDefaultStringInput();

        Assert.That(payload, Is.EqualTo("payload-a"));
    }

    [Test]
    public void RetrieveFirstOrDefaultStringOutput_WhenAllCachesAreEmpty_ReturnsNull()
    {
        var cache = new CompositeCache([new FakeCache(), new FakeCache()]);

        var payload = cache.RetrieveFirstOrDefaultStringOutput();

        Assert.That(payload, Is.Null);
    }

    [Test]
    public void Getters_WithNoCaches_ReturnFallbackDefaults()
    {
        var cache = new CompositeCache([]);

        Assert.Multiple(() =>
        {
            Assert.That(cache.EnableStorage, Is.False);
            Assert.That(cache.CachedAction, Is.Null);
            Assert.That(cache.InputDataFilter, Is.Not.Null);
            Assert.That(cache.OutputDataFilter, Is.Not.Null);
        });
    }

    [Test]
    public void CachedAction_Get_WhenFirstCacheHasNoAction_UsesNextCache()
    {
        var cache = new CompositeCache(
        [
            new FakeCache { CachedAction = null },
            new FakeCache { CachedAction = "ActionB" }
        ]);

        Assert.That(cache.CachedAction, Is.EqualTo("ActionB"));
    }

    [Test]
    public void RetrieveFirstOrDefaultStringOutput_WhenFirstCacheHasPayload_ReturnsIt()
    {
        var cache = new CompositeCache(
        [
            new FakeCache(outputs: ["payload-a"]),
            new FakeCache(outputs: ["payload-b"])
        ]);

        var payload = cache.RetrieveFirstOrDefaultStringOutput();

        Assert.That(payload, Is.EqualTo("payload-a"));
    }

    private sealed class FakeCache(IEnumerable<string>? inputs = null, IEnumerable<string>? outputs = null) : ICache
    {
        private readonly Queue<string> _inputs = new(inputs ?? []);
        private readonly Queue<string> _outputs = new(outputs ?? []);

        public bool EnableStorage { get; set; }
        public string? CachedAction { get; set; }
        public DataFilter InputDataFilter { get; set; } = new();
        public DataFilter OutputDataFilter { get; set; } = new();

        public string? RetrieveFirstOrDefaultStringInput() => _inputs.Count == 0 ? null : _inputs.Dequeue();
        public string? RetrieveFirstOrDefaultStringOutput() => _outputs.Count == 0 ? null : _outputs.Dequeue();
    }
}
