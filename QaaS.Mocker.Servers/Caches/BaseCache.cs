using QaaS.Framework.SDK.Session;

namespace QaaS.Mocker.Servers.Caches;

/// <summary>
/// Base implementation for caches that capture request and response payloads for controller consume commands.
/// </summary>
public abstract class BaseCache<TInput> : ICache
{
    public bool EnableStorage { get; set; }

    public string? CachedAction { get; set; }

    public DataFilter InputDataFilter { get; set; } = new();

    public DataFilter OutputDataFilter { get; set; } = new();

    /// <summary>
    /// Stores an inbound payload for later consumption.
    /// </summary>
    public abstract void StoreInput(TInput item, string actionName);

    /// <summary>
    /// Stores an outbound payload for later consumption.
    /// </summary>
    public abstract void StoreOutput(TInput item, string actionName);

    public abstract string? RetrieveFirstOrDefaultStringInput();

    public abstract string? RetrieveFirstOrDefaultStringOutput();
}
