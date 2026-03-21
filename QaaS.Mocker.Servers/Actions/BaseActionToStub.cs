namespace QaaS.Mocker.Servers.Actions;

/// <summary>
/// Associates a named runtime action with the backing object used to execute it.
/// </summary>
public abstract class BaseActionToStub<TStub>
{
    /// <summary>
    /// Gets or sets the logical action name used by controller commands and diagnostics.
    /// </summary>
    public string? ActionName { get; set; }

    /// <summary>
    /// Gets or sets the bound action implementation.
    /// </summary>
    public TStub Stub { get; set; } = default!;
}
