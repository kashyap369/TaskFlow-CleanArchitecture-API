namespace TaskFlow.Infra.Meetings;

public sealed class LiveKitSettings
{
    public const string SectionName = "LiveKit";

    public bool Enabled { get; set; }

    public string Url { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiSecret { get; set; } = string.Empty;

    public int WebhookToleranceSeconds { get; set; } = 300;
}
