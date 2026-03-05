
namespace QaaS.Mocker.Servers.Actions;

/// <summary>
/// Class that stores state for action of a Server, is enabled for applying and the matching stub.
/// </summary>
/// <typeparam name="TStateIndicator">State indicator object to rely on in the server's functionality.</typeparam>
public class ActionState<TStateIndicator> : ActionToTransactionStub
{
    public bool Enabled { get; set; }

    public TStateIndicator State { get; set; }

    /// <summary>
    /// Action will hold state of enabled for given interval in milliseconds in order to be triggered.
    /// </summary>
    public async Task SetEnabledForTimeoutMs(int timeoutMs)
    {
        Enabled = true;
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected cancellation when timeout elapsed.
        }
        finally
        {
            Enabled = false;
        }
    }
}
