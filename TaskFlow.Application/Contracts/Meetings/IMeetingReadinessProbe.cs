namespace TaskFlow.Application.Contracts.Meetings;

/// <summary>
/// Reports whether the meeting media stack is usable by the <b>running process</b>.
/// A deployment platform can save configuration without propagating it into the
/// container — that is what deferred the Meetings rollout on 2026-09-02, and it
/// surfaced only as a member-facing "LiveKit media is not enabled" at join time.
/// This probe answers the operator's question before anyone opens a room.
/// It proves local token signing and configuration shape only; media reachability
/// is still proven by the staging two-client call.
/// </summary>
public interface IMeetingReadinessProbe
{
    MeetingReadinessReport Describe();
}

public sealed record MeetingReadinessReport(
    string Status,
    bool MeetingsEnabled,
    bool GuestsEnabled,
    bool RecordingEnabled,
    bool LiveKitEnabled,
    string? WebSocketScheme,
    string? WebSocketHost,
    bool ApiKeyConfigured,
    string? ApiKeyFingerprint,
    bool ApiSecretConfigured,
    int ApiSecretLength,
    bool RecordingStorageConfigured,
    bool JoinTokenIssued,
    string? JoinTokenFailure,
    IReadOnlyList<string> Blockers,
    /// <summary>
    /// The ceilings this process enforces. An operator sizing a deployment, or explaining to a host
    /// why a start was refused, should not have to read the configuration file to find them.
    /// </summary>
    MeetingCapacity Capacity);

public static class MeetingReadinessStatus
{
    /// <summary>Media is configured and the process can sign a join token.</summary>
    public const string Ready = "Ready";

    /// <summary>Deliberately switched off — not a fault.</summary>
    public const string Disabled = "Disabled";

    /// <summary>Switched on but unusable; <c>Blockers</c> says why.</summary>
    public const string Misconfigured = "Misconfigured";
}
