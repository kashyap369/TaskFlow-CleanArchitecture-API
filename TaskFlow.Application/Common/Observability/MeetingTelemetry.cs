using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TaskFlow.Application.Common.Observability;

/// <summary>
/// Phase 7 / P7.4. The single vocabulary for every meeting signal — metrics, traces and the
/// structured log fields that go with them.
///
/// Two rules govern what may appear here, and both exist because meeting data is the most sensitive
/// data in TaskFlow:
///
/// 1. <b>No content, ever.</b> No email address, guest session token, access-link token, join token,
///    LiveKit room name, display name, meeting title, chat text, file name or IP address becomes a
///    tag or a log field. A telemetry pipeline is copied, cached and read by people who were never
///    admitted to the meeting; anything put here should be assumed to leave the building.
/// 2. <b>Metric tags are bounded; identifiers are not tags.</b> A meeting id, participant id or
///    organization id would give every meeting its own time series and eventually take the metrics
///    backend down. Identifiers belong on the trace/span and in the log line for the one request
///    that needs them — never on a counter. <see cref="Route"/> uses the ASP.NET route template for
///    the same reason: `/api/meeting/{meetingId}/messages`, not the concrete path.
///
/// This lives in Application rather than beside <c>PlannerTelemetry</c> in the API because meeting
/// signals do not originate at the HTTP edge: capacity refusals, join-token issuance, guest
/// verification and recording lifecycle are decided in handlers, and the LiveKit call outcomes in
/// Infra. Putting the instruments here lets all three layers speak the same vocabulary.
/// </summary>
public static class MeetingTelemetry
{
    public const string SourceName = "TaskFlow.Meetings";

    public static readonly ActivitySource ActivitySource = new(SourceName);
    public static readonly Meter Meter = new(SourceName);

    /// <summary>Every request that reached a meeting route, tagged by route template and outcome.</summary>
    public static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("taskflow.meetings.requests");

    public static readonly Histogram<double> RequestDuration =
        Meter.CreateHistogram<double>("taskflow.meetings.request.duration", "ms");

    /// <summary>Join-token issuance — the step that decides whether anyone can enter a room at all.</summary>
    public static readonly Counter<long> JoinTokens =
        Meter.CreateCounter<long>("taskflow.meetings.join.tokens");

    /// <summary>Guest email-verification funnel: inspect, code request, code verification.</summary>
    public static readonly Counter<long> GuestVerifications =
        Meter.CreateCounter<long>("taskflow.meetings.guest.verifications");

    /// <summary>LiveKit webhook deliveries, split by why one was refused.</summary>
    public static readonly Counter<long> Webhooks =
        Meter.CreateCounter<long>("taskflow.meetings.webhooks");

    /// <summary>Recording lifecycle transitions, including the ones nobody sees in the UI.</summary>
    public static readonly Counter<long> Recordings =
        Meter.CreateCounter<long>("taskflow.meetings.recordings");

    /// <summary>A declared ceiling refused a write. Tagged with the refusal code, not the meeting.</summary>
    public static readonly Counter<long> CapacityRefusals =
        Meter.CreateCounter<long>("taskflow.meetings.capacity.refusals");

    /// <summary>Calls this process made to LiveKit (room service and egress), with their outcome.</summary>
    public static readonly Counter<long> MediaCalls =
        Meter.CreateCounter<long>("taskflow.meetings.media.calls");

    public static readonly Histogram<double> MediaCallDuration =
        Meter.CreateHistogram<double>("taskflow.meetings.media.duration", "ms");

    // Tag keys. Constants because the aggregator, the tests and the alert rules all key off them,
    // and a typo in a tag name is a silently empty dashboard rather than a compile error.
    public static class Tags
    {
        public const string Route = "taskflow.meeting.route";
        public const string Method = "http.request.method";
        public const string StatusCode = "http.response.status_code";
        public const string StatusClass = "taskflow.meeting.status_class";
        public const string Actor = "taskflow.meeting.actor";
        public const string Outcome = "taskflow.meeting.outcome";
        public const string Stage = "taskflow.meeting.stage";
        public const string Event = "taskflow.meeting.event";
        public const string Limit = "taskflow.meeting.limit";
        public const string Operation = "taskflow.meeting.operation";
        public const string Reason = "taskflow.meeting.reason";
    }

    /// <summary>Who the request came from. Deliberately a class, never an identity.</summary>
    public static class Actors
    {
        public const string Member = "member";
        public const string Guest = "guest";
        public const string Webhook = "webhook";
        public const string Anonymous = "anonymous";
    }

    public static class Outcomes
    {
        public const string Issued = "issued";
        public const string Refused = "refused";
        public const string Accepted = "accepted";
        public const string Rejected = "rejected";
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
    }

    public static class StatusClasses
    {
        public const string Ok = "ok";
        public const string ClientError = "client_error";
        public const string Denied = "denied";
        public const string Throttled = "throttled";
        public const string ServerError = "server_error";
    }

    /// <summary>
    /// Maps an HTTP status to the five classes the alert rules reason about. `denied` is split out
    /// from `client_error` on purpose: a run of 401/403 on meeting routes is the shape of someone
    /// probing links and sessions, and it must not be diluted by ordinary validation failures.
    /// </summary>
    public static string ClassifyStatus(int statusCode) => statusCode switch
    {
        >= 500 => StatusClasses.ServerError,
        429 => StatusClasses.Throttled,
        401 or 403 => StatusClasses.Denied,
        >= 400 => StatusClasses.ClientError,
        _ => StatusClasses.Ok
    };
}

/// <summary>
/// The recording lifecycle points worth counting. These are transitions an operator cannot see in
/// the UI: a host who is refused sees a message, but a host whose recording silently never started
/// sees nothing, and neither does anyone in the room.
/// </summary>
public static class MeetingRecordingEvents
{
    public const string Requested = "requested";
    public const string ConsentPending = "consent_pending";
    public const string Started = "started";
    public const string StartFailed = "start_failed";
    public const string RosterUnavailable = "roster_unavailable";
    public const string Stopped = "stopped";
}

/// <summary>Stages of the guest email-verification funnel.</summary>
public static class MeetingGuestStages
{
    /// <summary>An access link was opened and inspected.</summary>
    public const string Inspect = "inspect";

    /// <summary>A one-time code was requested for an address.</summary>
    public const string RequestCode = "request_code";

    /// <summary>A code was submitted. A run of failures here is the abuse signal.</summary>
    public const string Verify = "verify";
}

/// <summary>Named LiveKit operations, so a failure says which call broke rather than only that one did.</summary>
public static class MeetingMediaOperations
{
    public const string ListParticipants = "list_participants";
    public const string RemoveParticipants = "remove_participants";
    public const string MuteParticipant = "mute_participant";
    public const string CloseRoom = "close_room";
    public const string StartRecording = "start_recording";
    public const string StopRecording = "stop_recording";
    public const string RecordingStatus = "recording_status";
}

/// <summary>
/// Why a LiveKit webhook delivery ended the way it did. Rejections are prefixed <c>rejected</c>
/// because the alert rule matches on that prefix: a rejected delivery means attendance, room
/// lifecycle or recording completion was never written, and the UI keeps looking healthy while the
/// database quietly falls behind. An <c>ignored</c> delivery is not a fault — it names a room this
/// deployment does not own, which is normal when several environments share a LiveKit server.
/// </summary>
public static class MeetingWebhookOutcomes
{
    public const string Accepted = "accepted";
    public const string RejectedSignature = "rejected_signature";
    public const string RejectedProcessing = "rejected_processing";
    public const string Duplicate = "duplicate";
    public const string Ignored = "ignored";
}
