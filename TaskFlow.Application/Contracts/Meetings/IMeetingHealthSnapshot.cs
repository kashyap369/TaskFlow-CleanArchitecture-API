namespace TaskFlow.Application.Contracts.Meetings;

/// <summary>
/// Phase 7 / P7.4. A rolling, in-process view of the meeting signals defined by
/// <c>MeetingTelemetry</c>, plus the alert rules evaluated against them.
///
/// TaskFlow emits standard .NET metrics, so a deployment with a collector should scrape those and
/// alert there. This exists because the production deployment does not have one, and an alert rule
/// nobody can evaluate is not an alert. It keeps a bounded ring of one-minute buckets in memory —
/// no persistence, no allocation per request beyond the bucket, and everything is lost on restart,
/// which is correct: it answers "what is happening now", not "what happened last Tuesday".
/// </summary>
public interface IMeetingHealthSnapshot
{
    MeetingHealthReport Describe(DateTime nowUtc);
}

/// <param name="GeneratedAtUtc">When this snapshot was taken.</param>
/// <param name="ObservingSinceUtc">
/// When collection started — process start, in practice. A window longer than this has not been
/// fully observed, and the report says so rather than implying a quiet hour that never happened.
/// </param>
/// <param name="Alerts">Every rule, firing or not, so a quiet system is visibly quiet rather than blank.</param>
/// <param name="Series">Raw counts per signal over each window, for the operator who wants the numbers.</param>
public sealed record MeetingHealthReport(
    DateTime GeneratedAtUtc,
    DateTime ObservingSinceUtc,
    bool FullyObserved,
    IReadOnlyList<MeetingHealthAlert> Alerts,
    IReadOnlyList<MeetingHealthSeries> Series,
    MeetingRequestLatency Latency);

/// <param name="Id">Stable rule id; also the runbook anchor.</param>
/// <param name="Severity">See <see cref="MeetingAlertSeverity"/>.</param>
/// <param name="Firing">Whether <paramref name="Observed"/> has reached <paramref name="Threshold"/>.</param>
/// <param name="Summary">What is wrong, in the operator's language, not the metric's.</param>
/// <param name="Runbook">Anchor in docs/MEETINGS-OBSERVABILITY.md that says what to do about it.</param>
public sealed record MeetingHealthAlert(
    string Id,
    string Severity,
    bool Firing,
    long Observed,
    long Threshold,
    int WindowMinutes,
    string Summary,
    string Runbook);

/// <param name="Signal">Metric name, e.g. <c>taskflow.meetings.webhooks</c>.</param>
/// <param name="Key">The low-cardinality tag value this row counts, e.g. <c>rejected_signature</c>.</param>
public sealed record MeetingHealthSeries(
    string Signal,
    string Key,
    long LastFiveMinutes,
    long LastFifteenMinutes,
    long LastHour);

/// <param name="MaxMilliseconds">Worst single request in the window — the number a user actually felt.</param>
public sealed record MeetingRequestLatency(
    long Requests,
    double AverageMilliseconds,
    double MaxMilliseconds,
    int WindowMinutes);

public static class MeetingAlertSeverity
{
    /// <summary>Meetings are broken or people are being recorded/admitted wrongly. Page someone.</summary>
    public const string Critical = "Critical";

    /// <summary>Degraded, or an abuse pattern worth a human look within the hour.</summary>
    public const string Warning = "Warning";
}
