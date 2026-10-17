namespace TaskFlow.Api.Models.Requests;

/// <summary>A punch: the selfie is required; the time is the server's, never the phone's.</summary>
public sealed class AttendancePunchRequest
{
    public string? Note { get; init; }
    public IFormFile? Photo { get; init; }
}

public sealed record AdjustAttendanceSessionRequest(DateTimeOffset PunchInAtUtc, DateTimeOffset PunchOutAtUtc, string Reason);

public sealed record UpdateAttendanceSettingsRequest(string TimeZone, int AutoCloseAfterHours, int PhotoRetentionDays);
