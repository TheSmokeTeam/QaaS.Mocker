using Microsoft.Extensions.Logging;

namespace QaaS.Mocker.Servers.Metrics;

internal enum HttpRequestOutcome { Hit, Miss, Error }

internal sealed class HttpRequestMetrics : IDisposable
{
    private readonly ILogger _logger;
    private readonly MockerLogIdentity _identity;
    private readonly int _port;
    private readonly MockerAttempt _attempt;
    private long _batchSequence;
    private int _flushInProgress;
    private readonly Guid _serverId = Guid.NewGuid();
    private readonly Timer _timer;
    // One increment per completion keeps the denominator and both rates consistent across flushes.
    private readonly long[] _counts = new long[6];

    internal HttpRequestMetrics(ILogger logger, MockerLogIdentity identity, int port)
    {
        _logger = logger;
        _identity = identity;
        _attempt = identity.CurrentAttempt ?? identity.NewAttempt(false, false, MockerLogIdentity.GetVersion(typeof(HttpRequestMetrics).Assembly));
        _port = port;
        var period = TimeSpan.FromSeconds(10);
        _timer = new Timer(_ => Flush(), null, period, period);
    }

    internal void Complete(HttpRequestOutcome outcome, int statusCode)
    {
        var index = (int)outcome * 2 + (statusCode is >= 500 and < 600 ? 1 : 0);
        Interlocked.Increment(ref _counts[index]);
    }

    internal void Flush()
    {
        // A slow provider must not start overlapping writes or drain a newer window out of order.
        // Completions remain in the counters for the next successful acquisition.
        if (Interlocked.CompareExchange(ref _flushInProgress, 1, 0) != 0)
            return;

        try
        {
            FlushWindow();
        }
        finally
        {
            Volatile.Write(ref _flushInProgress, 0);
        }
    }

    private void FlushWindow()
    {
        Span<long> counts = stackalloc long[6];
        long requests = 0;
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = Interlocked.Exchange(ref _counts[i], 0);
            requests += counts[i];
        }

        if (requests == 0)
            return;

        try
        {
            _logger.LogDebug(
                "{QaasEvent}: {Requests} HTTP requests, {HandlerErrors} handler errors, {Misses} unmatched, {Http5xx} HTTP 5xx (ServerId: {ServerId}, Port: {Port}, BatchSequence: {BatchSequence}) " + MockerLogIdentity.Labels,
                "MockerHttpRequests", requests, counts[4] + counts[5], counts[2] + counts[3],
                counts[1] + counts[3] + counts[5], _serverId, _port, ++_batchSequence,
                _attempt.Id, _identity.Team, _identity.Project, _identity.System,
                _identity.TeamKnown, _identity.ProjectKnown, _identity.SystemKnown, _attempt.ExecutionType,
                _attempt.Version, _attempt.Features);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not AccessViolationException)
        {
            // A host-supplied logging provider can throw. A timer callback must not terminate the mock process.
        }
    }

    public void Dispose()
    {
        _timer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Flush();
    }
}
