using QaaS.Framework.SDK.Session;

namespace QaaS.Mocker.Servers.Caches;

/// <summary>
/// Aggregates multiple server caches into a single controller-facing cache surface.
/// </summary>
public sealed class CompositeCache(IEnumerable<ICache> caches) : ICache
{
    private readonly ICache[] _caches = caches.ToArray();

    public bool EnableStorage
    {
        get => _caches.Any(cache => cache.EnableStorage);
        set
        {
            foreach (var cache in _caches)
                cache.EnableStorage = value;
        }
    }

    public string? CachedAction
    {
        get => _caches.Select(cache => cache.CachedAction).FirstOrDefault(action => action != null);
        set
        {
            foreach (var cache in _caches)
                cache.CachedAction = value;
        }
    }

    public DataFilter InputDataFilter
    {
        get => _caches.Select(cache => cache.InputDataFilter).FirstOrDefault() ?? new DataFilter();
        set
        {
            foreach (var cache in _caches)
                cache.InputDataFilter = value;
        }
    }

    public DataFilter OutputDataFilter
    {
        get => _caches.Select(cache => cache.OutputDataFilter).FirstOrDefault() ?? new DataFilter();
        set
        {
            foreach (var cache in _caches)
                cache.OutputDataFilter = value;
        }
    }

    public string? RetrieveFirstOrDefaultStringInput()
    {
        foreach (var cache in _caches)
        {
            var payload = cache.RetrieveFirstOrDefaultStringInput();
            if (payload != null)
                return payload;
        }

        return null;
    }

    public string? RetrieveFirstOrDefaultStringOutput()
    {
        foreach (var cache in _caches)
        {
            var payload = cache.RetrieveFirstOrDefaultStringOutput();
            if (payload != null)
                return payload;
        }

        return null;
    }
}
