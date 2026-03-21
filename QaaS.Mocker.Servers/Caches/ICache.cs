using QaaS.Framework.SDK.Session;

namespace QaaS.Mocker.Servers.Caches;

/// <summary>
/// Defines the controller-facing cache contract for captured request and response payloads.
/// </summary>
public interface ICache
{
    /// <summary>
    /// Gets or sets whether payload capture is currently enabled.
    /// </summary>
    public bool EnableStorage { get; set; }

    /// <summary>
    /// Gets or sets the action whose traffic should be captured, or <see langword="null"/> for all actions.
    /// </summary>
    public string? CachedAction { get; set; }

    /// <summary>
    /// Gets or sets the filter applied to captured request payloads.
    /// </summary>
    public DataFilter InputDataFilter { get; set; }

    /// <summary>
    /// Gets or sets the filter applied to captured response payloads.
    /// </summary>
    public DataFilter OutputDataFilter { get; set; }

    /// <summary>
    /// Retrieves and removes the next cached request payload in serialized form.
    /// </summary>
    public string? RetrieveFirstOrDefaultStringInput();

    /// <summary>
    /// Retrieves and removes the next cached response payload in serialized form.
    /// </summary>
    public string? RetrieveFirstOrDefaultStringOutput();
}
