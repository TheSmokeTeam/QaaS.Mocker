using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace QaaS.Mocker.Servers.Metrics;

internal enum HttpRequestOutcome
{
    Hit,
    Miss,
    Error
}

/// <summary>
/// Process-wide instruments. Only HTTP servers which opted in publish measurements.
/// No instrument opens a port, controls routing, or installs an exporter.
/// </summary>
internal sealed class HttpRequestMetrics
{
    private static readonly Lazy<HttpRequestMetrics?> Shared = new(TryCreate);
    internal static HttpRequestMetrics? Instance => Shared.Value;

    private readonly Meter _meter;
    private readonly Histogram<double> _duration;
    private long _accepted;
    private long _hits;
    private long _misses;
    private long _errors;

    private HttpRequestMetrics(Meter meter)
    {
        _meter = meter;
        _meter.CreateObservableCounter("qaas.mocker.requests.accepted", () => Interlocked.Read(ref _accepted),
            description: "HTTP requests entering the opted-in mock handler.");
        _meter.CreateObservableCounter("qaas.mocker.requests.completed", ObserveCompleted,
            description: "HTTP requests leaving the handler, partitioned by dispatch outcome.");
        _duration = _meter.CreateHistogram<double>("qaas.mocker.request.duration", "s",
            "Monotonic time in the mock HTTP handler, in seconds.", tags: null,
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 15]
            });
    }

    internal static HttpRequestMetrics? TryCreate()
    {
        Meter? meter = null;
        try
        {
            meter = new Meter("QaaS.Mocker.Http");
            return new HttpRequestMetrics(meter);
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // A host-owned listener can throw during instrument publication.
            try { meter?.Dispose(); }
            catch (Exception disposeException) when (IsNonFatal(disposeException)) { }
            return null;
        }
    }

    private static bool IsNonFatal(Exception exception)
        => exception is not (OutOfMemoryException or AccessViolationException);

    // Zero values are known even to a listener which attaches after server creation.
    private IEnumerable<Measurement<long>> ObserveCompleted()
    {
        yield return new Measurement<long>(Interlocked.Read(ref _hits), KeyValuePair.Create<string, object?>("outcome", "hit"));
        yield return new Measurement<long>(Interlocked.Read(ref _misses), KeyValuePair.Create<string, object?>("outcome", "miss"));
        yield return new Measurement<long>(Interlocked.Read(ref _errors), KeyValuePair.Create<string, object?>("outcome", "error"));
    }

    internal long Start()
    {
        Interlocked.Increment(ref _accepted);
        return Stopwatch.GetTimestamp();
    }

    internal void Complete(HttpRequestOutcome outcome, long started)
    {
        var elapsedSeconds = Stopwatch.GetElapsedTime(started).TotalSeconds;
        switch (outcome)
        {
            case HttpRequestOutcome.Hit:
                Interlocked.Increment(ref _hits);
                break;
            case HttpRequestOutcome.Miss:
                Interlocked.Increment(ref _misses);
                break;
            default:
                Interlocked.Increment(ref _errors);
                break;
        }

        try
        {
            _duration.Record(elapsedSeconds);
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // A host-owned in-process listener may throw; metrics must not break the handler.
        }
    }
}
