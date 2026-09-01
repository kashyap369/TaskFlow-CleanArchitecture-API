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
}
