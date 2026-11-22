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
/// Counter state belongs to one opted-in HTTP server. The bounded publisher
/// registers the instruments once so an exporter cannot merge unlabeled owners.
/// </summary>
internal sealed class HttpRequestMetrics
{
    internal const int MinSlot = 1;
    internal const int MaxSlot = 8;
    private static readonly HttpRequestMetrics?[] Owners = new HttpRequestMetrics?[MaxSlot];
    private static readonly Lazy<Publisher?> SharedPublisher = new(TryCreatePublisher);

    private static readonly KeyValuePair<string, object?> HitTag = new("outcome", "hit");
    private static readonly KeyValuePair<string, object?> MissTag = new("outcome", "miss");
    private static readonly KeyValuePair<string, object?> ErrorTag = new("outcome", "error");

    private readonly KeyValuePair<string, object?> _slotTag;
    private long _accepted;
    private long _hits;
    private long _misses;
    private long _errors;

    private HttpRequestMetrics(int slot) => _slotTag = new("metric_slot", slot);

    internal static HttpRequestMetrics? Register(int? slot)
    {
        if (slot is < MinSlot or > MaxSlot or null)
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"Enabled HTTP metrics require HttpMetricsSlot from {MinSlot} to {MaxSlot}.");

        var owner = new HttpRequestMetrics(slot.Value);
        // Do not reassign a time-series identity inside the same process. A stopped
        // server's four counters stay frozen under its slot until process restart.
        if (Interlocked.CompareExchange(ref Owners[slot.Value - MinSlot], owner, null) is not null)
            throw new InvalidOperationException($"HTTP metrics slot {slot} is already assigned in this process.");

        return SharedPublisher.Value is null ? null : owner;
    }

    internal static Publisher? TryCreatePublisher()
    {
        Meter? meter = null;
        try
        {
            meter = new Meter("QaaS.Mocker.Http");
            return new Publisher(meter);
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // A host-owned listener can throw during instrument publication.
            try
            {
                meter?.Dispose();
            }
            catch (Exception disposeException) when (IsNonFatal(disposeException)) { }
            return null;
        }
    }

    private static bool IsNonFatal(Exception exception)
        => exception is not (OutOfMemoryException or AccessViolationException);

    private static IEnumerable<HttpRequestMetrics> ObserveOwners()
    {
        for (var i = 0; i < Owners.Length; ++i)
        {
            if (Volatile.Read(ref Owners[i]) is { } owner)
                yield return owner;
        }
    }

    private static IEnumerable<Measurement<long>> ObserveAccepted()
    {
        foreach (var owner in ObserveOwners())
            yield return new Measurement<long>(Interlocked.Read(ref owner._accepted), owner._slotTag);
    }

    private static IEnumerable<Measurement<long>> ObserveCompleted()
    {
        foreach (var owner in ObserveOwners())
        {
            yield return new Measurement<long>(Interlocked.Read(ref owner._hits), owner._slotTag, HitTag);
            yield return new Measurement<long>(Interlocked.Read(ref owner._misses), owner._slotTag, MissTag);
            yield return new Measurement<long>(Interlocked.Read(ref owner._errors), owner._slotTag, ErrorTag);
        }
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
            SharedPublisher.Value?.Duration.Record(elapsedSeconds, _slotTag);
        }
        catch (Exception exception) when (IsNonFatal(exception))
        {
            // A host-owned in-process listener may throw; metrics must not break the handler.
        }
    }

    internal sealed class Publisher
    {
        internal Histogram<double> Duration { get; }

        internal Publisher(Meter meter)
        {
            meter.CreateObservableCounter("qaas.mocker.requests.accepted", ObserveAccepted,
                description: "HTTP requests entering the opted-in mock handler.");
            meter.CreateObservableCounter("qaas.mocker.requests.completed", ObserveCompleted,
                description: "HTTP requests leaving the handler, partitioned by dispatch outcome.");
            Duration = meter.CreateHistogram<double>("qaas.mocker.request.duration", "s",
                "Monotonic time in the mock HTTP handler, in seconds.", tags: null,
                advice: new InstrumentAdvice<double>
                {
                    HistogramBucketBoundaries = [0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 15]
                });
        }
    }
}
