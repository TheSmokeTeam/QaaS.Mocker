
using QaaS.Framework.SDK.Session;

namespace QaaS.Mocker.Servers.Caches;

public abstract class BaseCache<TInput> : ICache
{ 
    public bool EnableStorage { get; set; }
    public string? CachedAction { get; set; }
    public DataFilter InputDataFilter { get; set; } = new();
    public DataFilter OutputDataFilter { get; set; } = new();

    public abstract void StoreInput(TInput item, string actionName);
    public abstract void StoreOutput(TInput item, string actionName);
    public abstract string? RetrieveFirstOrDefaultStringInput();
    public abstract string? RetrieveFirstOrDefaultStringOutput();
}