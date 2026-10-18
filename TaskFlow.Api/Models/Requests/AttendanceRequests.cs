namespace TaskFlow.Api.Models.Requests;

/// <summary>A punch: the selfie is required; the time is the server's, never the phone's. Location is optional.</summary>
public sealed class AttendancePunchRequest
{
    public string? Note { get; init; }
    public IFormFile? Photo { get; init; }
    // Form fields: send with a '.' decimal separator.
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
}

public sealed record AdjustAttendanceSessionRequest(DateTimeOffset PunchInAtUtc, DateTimeOffset PunchOutAtUtc, string Reason);

public sealed record UpdateAttendanceSettingsRequest(string TimeZone, int AutoCloseAfterHours, int PhotoRetentionDays);

public sealed record RecordLocationPointsRequest(int? AttendanceSessionId, IReadOnlyList<LocationPointRequest> Points);

public sealed record LocationPointRequest(DateTimeOffset RecordedAt, double Latitude, double Longitude, double? AccuracyMeters,
    double? SpeedMetersPerSecond, double? HeadingDegrees, bool IsMock, int? BatteryPercent, bool? IsCharging,
    bool? IsBatterySaverOn);

public sealed record GiveFieldTrackingConsentRequest(int PolicyVersion);

public sealed record UpdateFieldTrackingSettingsRequest(bool TrackingEnabled, int LocationRetentionDays,
    IReadOnlyList<int> TrackedRoleIds);
