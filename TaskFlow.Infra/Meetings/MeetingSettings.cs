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
}
