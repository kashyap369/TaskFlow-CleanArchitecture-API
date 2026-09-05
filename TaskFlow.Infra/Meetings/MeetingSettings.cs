namespace TaskFlow.Infra.Meetings;

public sealed class MeetingSettings
{
    public const string SectionName = "Meetings";
    public bool Enabled { get; set; }
    public bool GuestsEnabled { get; set; }
    public bool RecordingEnabled { get; set; }
    public int GuestSessionMinutes { get; set; } = 60;
    public int DefaultRetentionDays { get; set; } = 90;
    public long MaxFileBytes { get; set; } = 25 * 1024 * 1024;
    public int RecordingConsentTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// How long a participant must have been in the room before the provider's `room_finished`
    /// event may end the meeting. Below this, the room emptying is treated as a failed call and the
    /// meeting stays open so the host can retry rather than finding it archived.
    /// </summary>
    public int AutoEndMinimumSessionSeconds { get; set; } = 30;

    // ---- Declared capacity (Phase 7 / P7.3) ---------------------------------------------------
    // TaskFlow states limits it can defend rather than implying unlimited scale. Every value below
    // is refused server-side when reached. These are the conservative defaults MEETINGS.md §12
    // prescribes until the owner approves real numbers; docs/MEETINGS-CAPACITY.md explains each.

    /// <summary>Seats on one meeting's roster, host included. Only assigned participants get a join token.</summary>
    public int MaxParticipantsPerMeeting { get; set; } = 50;

    /// <summary>Meetings one organization may hold Live at the same time.</summary>
    public int MaxConcurrentLiveMeetingsPerOrganization { get; set; } = 10;

    /// <summary>
    /// Egress jobs the deployment may run at once, across every organization. Recording is CPU-bound
    /// and shared: one 720p room-composite job per 4 vCPU / 4 GB worker is the documented policy, so
    /// this is a deployment-wide ceiling rather than a per-meeting one.
    /// </summary>
    public int MaxConcurrentRecordings { get; set; } = 1;

    /// <summary>Chat messages retained for one meeting.</summary>
    public int MaxMessagesPerMeeting { get; set; } = 5000;

    /// <summary>Files shared in one meeting.</summary>
    public int MaxAssetsPerMeeting { get; set; } = 100;

    /// <summary>
    /// Total shared-file bytes for one meeting. Until now this was an implicit
    /// <c>MaxFileBytes * 10</c> inside the upload handler; it is declared configuration now so the
    /// number can be stated, tested and tuned without editing a handler.
    /// </summary>
    public long MaxStorageBytesPerMeeting { get; set; } = 250 * 1024 * 1024;

    /// <summary>
    /// How long spent guest sessions and OTP challenges are kept after they expire. They are access
    /// records, not content, so meeting retention never reached them and nothing ever deleted them.
    /// Guest <i>decisions</i> are deliberately excluded: they are the moderation audit trail.
    /// </summary>
    public int GuestAccessRecordRetentionDays { get; set; } = 30;
}
