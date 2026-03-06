
namespace QaaS.Mocker.Servers.Actions;

/// <summary>
/// Class that stores state for action of a Server, is enabled for applying and the matching stub.
/// </summary>
/// <typeparam name="TStateIndicator">State indicator object to rely on in the server's functionality.</typeparam>
public class ActionState<TStateIndicator> : ActionToTransactionStub
{
    private readonly Lock _syncLock = new();
    private CancellationTokenSource? _disableCancellation;
    private long _activationVersion;
    private int _enabledState;

    /// <summary>
    /// Baseline enablement for the action when no temporary trigger window is active.
    /// Collect endpoints use <c>true</c> so they can receive immediately, while broadcast endpoints
    /// typically remain disabled until an explicit trigger command arrives.
    /// </summary>
    public bool DefaultEnabled { get; init; }

    /// <summary>
    /// Thread-safe enabled flag used by the server runtime.
    /// </summary>
    public bool Enabled
    {
        get => Volatile.Read(ref _enabledState) == 1;
        set => Volatile.Write(ref _enabledState, value ? 1 : 0);
    }

    public TStateIndicator State { get; set; }

    /// <summary>
    /// Temporarily enables the action for the given interval in milliseconds.
    /// Newer trigger commands supersede older ones, so only the latest activation window is allowed
    /// to restore the action back to <see cref="DefaultEnabled"/>.
    /// </summary>
    public async Task SetEnabledForTimeoutMs(int timeoutMs)
    {
        CancellationToken cancellationToken;
        long activationVersion;

        using (_syncLock.EnterScope())
        {
            // Replace the previous timeout source so an older trigger cannot disable a newer one.
            _disableCancellation?.Cancel();
            _disableCancellation?.Dispose();
            _disableCancellation = new CancellationTokenSource();
            cancellationToken = _disableCancellation.Token;
            activationVersion = ++_activationVersion;
            Enabled = true;
        }

        if (timeoutMs <= 0)
        {
            DisableIfCurrentActivation(activationVersion, cancellationToken);
            return;
        }

        try
        {
            await Task.Delay(timeoutMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer trigger supersedes the current one.
        }
        finally
        {
            DisableIfCurrentActivation(activationVersion, cancellationToken);
        }
    }

    private void DisableIfCurrentActivation(long activationVersion, CancellationToken cancellationToken)
    {
        using (_syncLock.EnterScope())
        {
            // Ignore completions from superseded trigger windows.
            if (activationVersion != _activationVersion || cancellationToken.IsCancellationRequested)
                return;

            _disableCancellation?.Dispose();
            _disableCancellation = null;
            Enabled = DefaultEnabled;
        }
    }
}
