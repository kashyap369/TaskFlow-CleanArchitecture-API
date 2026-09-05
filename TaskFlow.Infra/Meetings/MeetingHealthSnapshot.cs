using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using TaskFlow.Application.Common.Observability;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Infra.Meetings;

/// <summary>
/// Phase 7 / P7.4. Listens to the <see cref="MeetingTelemetry"/> meter and keeps the last hour of
/// counts in memory as one-minute buckets, then evaluates the alert rules documented in
/// docs/MEETINGS-OBSERVABILITY.md against them.
///
/// Why a listener rather than an exporter: the production deployment has no metrics collector, and
/// an alert rule nothing evaluates is not an alert. When a collector is added, the same instruments
/// feed it and these thresholds transfer unchanged — that is the point of keeping the rule
/// definitions in one table rather than scattered through dashboards.
///
/// Memory is bounded by construction: 60 buckets per series, and a series only exists for a
/// low-cardinality tag combination, because <see cref="MeetingTelemetry"/> forbids identifiers as
/// tags. Nothing here is persisted and a restart starts a fresh window, which
/// <see cref="MeetingHealthReport.FullyObserved"/> reports honestly rather than showing a quiet
/// hour that was never watched.
///
/// It is an <see cref="IHostedService"/> only so that the host constructs it at startup. Registered
/// as a plain singleton it would be built on the first read of the health endpoint, and would then
/// report an empty window over a system that had been serving meetings for hours — a listener that
/// starts when someone asks about it has already missed what they are asking about.
/// </summary>
public sealed class MeetingHealthSnapshot : IMeetingHealthSnapshot, IHostedService, IDisposable
{
    private const int BucketCount = 60;

    private readonly MeterListener _listener;
    private readonly ConcurrentDictionary<string, RollingSeries> _series = new(StringComparer.Ordinal);
    private readonly RollingLatency _latency = new();
    private readonly Func<DateTime> _clock;

    public DateTime ObservingSinceUtc { get; }

    public MeetingHealthSnapshot(Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.UtcNow);
        ObservingSinceUtc = _clock();

        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == MeetingTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>(OnCount);
        _listener.SetMeasurementEventCallback<double>(OnDuration);
        _listener.Start();
    }

    private void OnCount(Instrument instrument, long measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        var key = SeriesKey(instrument.Name, tags);
        if (key is null) return;
        _series.GetOrAdd(key, _ => new RollingSeries()).Add(measurement, Stamp(_clock()));
    }

    private void OnDuration(Instrument instrument, double measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument.Name != MeetingTelemetry.RequestDuration.Name) return;
        _latency.Add(measurement, Stamp(_clock()));
    }

    /// <summary>
    /// Projects a measurement's tags onto the one low-cardinality dimension each signal is alerted
    /// on. An instrument that is not listed is ignored rather than stored, so adding a richly
    /// tagged instrument later cannot quietly grow this dictionary.
    /// </summary>
    private static string? SeriesKey(string instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // A ref-like span cannot be captured by a lambda or a local function, so the tag lookup
        // takes the span as an explicit argument.
        string? key;
        if (instrument == MeetingTelemetry.Requests.Name)
        {
            key = TagValue(tags, MeetingTelemetry.Tags.StatusClass);
        }
        else if (instrument == MeetingTelemetry.JoinTokens.Name ||
                 instrument == MeetingTelemetry.Webhooks.Name)
        {
            key = TagValue(tags, MeetingTelemetry.Tags.Outcome);
        }
        else if (instrument == MeetingTelemetry.GuestVerifications.Name)
        {
            key = Join(TagValue(tags, MeetingTelemetry.Tags.Stage), TagValue(tags, MeetingTelemetry.Tags.Outcome));
        }
        else if (instrument == MeetingTelemetry.Recordings.Name)
        {
            key = TagValue(tags, MeetingTelemetry.Tags.Event);
        }
        else if (instrument == MeetingTelemetry.CapacityRefusals.Name)
        {
            key = TagValue(tags, MeetingTelemetry.Tags.Limit);
        }
        else if (instrument == MeetingTelemetry.MediaCalls.Name)
        {
            key = Join(TagValue(tags, MeetingTelemetry.Tags.Operation), TagValue(tags, MeetingTelemetry.Tags.Outcome));
        }
        else
        {
            key = null;
        }

        return key is null ? null : $"{instrument}|{key}";

        static string? Join(string? left, string? right) =>
            left is null || right is null ? null : $"{left}:{right}";
    }

    private static string? TagValue(ReadOnlySpan<KeyValuePair<string, object?>> tags, string name)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == name) return tag.Value?.ToString();
        }

        return null;
    }

    public MeetingHealthReport Describe(DateTime nowUtc)
    {
        var stamp = Stamp(nowUtc);

        long Sum(string signal, Func<string, bool> keyMatches, int minutes) =>
            _series.Where(entry =>
                {
                    var (rowSignal, rowKey) = Split(entry.Key);
                    return rowSignal == signal && keyMatches(rowKey);
                })
                .Sum(entry => entry.Value.Sum(stamp, minutes));

        long Exact(string signal, string key, int minutes) =>
            Sum(signal, candidate => candidate == key, minutes);

        var alerts = new List<MeetingHealthAlert>
        {
            // LiveKit is unreachable or its credentials are wrong. Nobody can be moderated out of a
            // room and no recording can start; this is the shape of the failure that deferred the
            // rollout on 2026-09-02.
            Rule("media_calls_failing", MeetingAlertSeverity.Critical,
                Sum(MeetingTelemetry.MediaCalls.Name,
                    key => key.EndsWith(":failed", StringComparison.Ordinal), 5),
                3, 5,
                "Calls to LiveKit are failing. Rooms cannot be moderated and recordings cannot start."),

            // A meeting route threw. Unhandled failures in this area strand people in a lobby with
            // no way forward, so one is worth looking at.
            Rule("server_errors", MeetingAlertSeverity.Critical,
                Exact(MeetingTelemetry.Requests.Name, MeetingTelemetry.StatusClasses.ServerError, 5),
                1, 5,
                "Meeting requests are returning 5xx."),

            // The recording either could not start or could not establish who was in the room. A
            // host may believe a meeting is being recorded when it is not, or consent may be owed
            // to people the roster never named.
            Rule("recording_failures", MeetingAlertSeverity.Critical,
                Sum(MeetingTelemetry.Recordings.Name,
                    key => key is MeetingRecordingEvents.StartFailed
                        or MeetingRecordingEvents.RosterUnavailable, 15),
                1, 15,
                "A recording failed to start, or the live roster could not be read for consent."),

            // Rejected webhooks mean attendance, room lifecycle and recording completion stop being
            // written. The UI keeps working, which is what makes this one dangerous.
            Rule("webhooks_rejected", MeetingAlertSeverity.Critical,
                Sum(MeetingTelemetry.Webhooks.Name,
                    key => key.StartsWith("rejected", StringComparison.Ordinal), 15),
                5, 15,
                "LiveKit webhooks are being rejected, so attendance and recording state are going stale."),

            // Refusals are normal in ones and twos: revoked access, a meeting that already ended. A
            // run of them is a misconfigured media stack or a link being worked over.
            Rule("join_tokens_refused", MeetingAlertSeverity.Warning,
                Exact(MeetingTelemetry.JoinTokens.Name, MeetingTelemetry.Outcomes.Refused, 5),
                10, 5,
                "Join tokens are being refused repeatedly."),

            // The shape of someone guessing six-digit codes against a leaked link.
            Rule("guest_verification_failures", MeetingAlertSeverity.Warning,
                Exact(MeetingTelemetry.GuestVerifications.Name,
                    $"{MeetingGuestStages.Verify}:{MeetingTelemetry.Outcomes.Failed}", 15),
                20, 15,
                "Guest verification codes are failing often, which is what code guessing looks like."),

            // Either the declared ceilings are too low for real use, or something is hammering them.
            Rule("capacity_refusals", MeetingAlertSeverity.Warning,
                Sum(MeetingTelemetry.CapacityRefusals.Name, _ => true, 15),
                5, 15,
                "Declared meeting capacity is refusing writes; review docs/MEETINGS-CAPACITY.md."),

            Rule("throttling", MeetingAlertSeverity.Warning,
                Exact(MeetingTelemetry.Requests.Name, MeetingTelemetry.StatusClasses.Throttled, 15),
                25, 15,
                "Meeting requests are being rate limited.")
        };

        var series = _series
            .Select(entry =>
            {
                var (signal, key) = Split(entry.Key);
                return new MeetingHealthSeries(signal, key,
                    entry.Value.Sum(stamp, 5), entry.Value.Sum(stamp, 15), entry.Value.Sum(stamp, 60));
            })
            .Where(row => row.LastHour > 0)
            .OrderBy(row => row.Signal, StringComparer.Ordinal)
            .ThenBy(row => row.Key, StringComparer.Ordinal)
            .ToList();

        return new MeetingHealthReport(
            nowUtc,
            ObservingSinceUtc,
            nowUtc - ObservingSinceUtc >= TimeSpan.FromMinutes(BucketCount),
            alerts,
            series,
            _latency.Describe(stamp, 15));
    }

    private static MeetingHealthAlert Rule(string id, string severity, long observed, long threshold,
        int windowMinutes, string summary) =>
        new(id, severity, observed >= threshold, observed, threshold, windowMinutes, summary, $"#{id}");

    private static (string Signal, string Key) Split(string composite)
    {
        var index = composite.IndexOf('|');
        return (composite[..index], composite[(index + 1)..]);
    }

    private static long Stamp(DateTime utc) => utc.Ticks / TimeSpan.TicksPerMinute;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose() => _listener.Dispose();

    /// <summary>
    /// A fixed ring of one-minute buckets. Each bucket remembers which minute it holds, so a bucket
    /// last written an hour ago reads as empty instead of being counted a second time — no
    /// background timer, and no unbounded growth for a series that goes quiet.
    /// </summary>
    private sealed class RollingSeries
    {
        private readonly long[] _stamps = new long[BucketCount];
        private readonly long[] _values = new long[BucketCount];
        private readonly object _gate = new();

        public void Add(long value, long stamp)
        {
            var index = Index(stamp);
            lock (_gate)
            {
                if (_stamps[index] != stamp)
                {
                    _stamps[index] = stamp;
                    _values[index] = 0;
                }

                _values[index] += value;
            }
        }

        public long Sum(long nowStamp, int minutes)
        {
            var oldest = nowStamp - minutes + 1;
            var total = 0L;
            lock (_gate)
            {
                for (var i = 0; i < BucketCount; i++)
                {
                    if (_stamps[i] >= oldest && _stamps[i] <= nowStamp) total += _values[i];
                }
            }

            return total;
        }
    }

    private sealed class RollingLatency
    {
        private readonly long[] _stamps = new long[BucketCount];
        private readonly long[] _counts = new long[BucketCount];
        private readonly double[] _sums = new double[BucketCount];
        private readonly double[] _maxes = new double[BucketCount];
        private readonly object _gate = new();

        public void Add(double milliseconds, long stamp)
        {
            var index = Index(stamp);
            lock (_gate)
            {
                if (_stamps[index] != stamp)
                {
                    _stamps[index] = stamp;
                    _counts[index] = 0;
                    _sums[index] = 0;
                    _maxes[index] = 0;
                }

                _counts[index]++;
                _sums[index] += milliseconds;
                if (milliseconds > _maxes[index]) _maxes[index] = milliseconds;
            }
        }

        public MeetingRequestLatency Describe(long nowStamp, int minutes)
        {
            var oldest = nowStamp - minutes + 1;
            long count = 0;
            double sum = 0, max = 0;
            lock (_gate)
            {
                for (var i = 0; i < BucketCount; i++)
                {
                    if (_stamps[i] < oldest || _stamps[i] > nowStamp) continue;
                    count += _counts[i];
                    sum += _sums[i];
                    if (_maxes[i] > max) max = _maxes[i];
                }
            }

            return new MeetingRequestLatency(count, count == 0 ? 0 : Math.Round(sum / count, 1),
                Math.Round(max, 1), minutes);
        }
    }

    private static int Index(long stamp) => (int)(((stamp % BucketCount) + BucketCount) % BucketCount);
}
