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

    private const int Reserved = 0;
    private const int Active = 1;
    private const int Released = 2;
    private int _state;
    private readonly int _ownerIndex;
    private Publisher? _publisher;

    private HttpRequestMetrics(int slot)
    {
        _ownerIndex = slot - MinSlot;
        _slotTag = new("metric_slot", slot);
    }

    internal static int ValidateAvailableSlot(int? slot)
    {
        if (slot is < MinSlot or > MaxSlot or null)
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"Enabled HTTP metrics require HttpMetricsSlot from {MinSlot} to {MaxSlot}.");
        if (Volatile.Read(ref Owners[slot.Value - MinSlot]) is not null)
            throw DuplicateSlot(slot.Value);
        return slot.Value;
    }

    internal static HttpRequestMetrics Reserve(int slot)
    {
        var owner = new HttpRequestMetrics(slot);
        // Constructors can coexist before startup; exactly one startup can own the slot.
        if (Interlocked.CompareExchange(ref Owners[owner._ownerIndex], owner, null) is not null)
            throw DuplicateSlot(slot);
        try
        {
            // Once the listener exists it can see active owners, but never a pre-bind reservation.
            // Initialize before the host can accept a request: observer callbacks cannot block
            // a live handler that would otherwise need to wait on this Lazy initialization.
            owner._publisher = SharedPublisher.Value;
            return owner;
        }
        catch
        {
            owner.ReleaseUnactivated();
            throw;
        }
    }

    private static InvalidOperationException DuplicateSlot(int slot) =>
        new($"HTTP metrics slot {slot} is already assigned in this process.");

    internal bool Activate()
    {
        // A handler can see a real request just before IHost.Start returns. The first activation
        // wins against failed-start cleanup; once active an identity can never go backwards/reset.
        if (Volatile.Read(ref _state) != Active &&
            Interlocked.CompareExchange(ref _state, Active, Reserved) == Released)
            return false;
        return true;
    }

    internal void ReleaseUnactivated()
    {
        if (Interlocked.CompareExchange(ref _state, Released, Reserved) == Reserved)
            Interlocked.CompareExchange(ref Owners[_ownerIndex], null, this);
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
            if (Volatile.Read(ref Owners[i]) is { } owner && Volatile.Read(ref owner._state) == Active)
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

    internal long? Start()
    {
        if (!Activate() || _publisher is null)
            return null;
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
            _publisher?.Duration.Record(elapsedSeconds, _slotTag);
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
