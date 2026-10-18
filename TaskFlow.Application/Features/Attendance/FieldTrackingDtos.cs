using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Attendance;

/// <summary>The policy text a tracked member accepts is versioned in the clients; bump this when it changes.</summary>
public static class FieldTrackingPolicy
{
    public const int CurrentVersion = 1;
    // What the app is asked to do. Server-side so it can be tuned without shipping an app.
    public const int MovingIntervalSeconds = 5;
    public const int MovingDistanceMeters = 10;
    public const int StillIntervalSeconds = 60;
    public const int UploadIntervalSeconds = 120;
    public const int MaxPointsPerUpload = 1000;
    // A phone that was offline at punch-out may still deliver that session's points this long after.
    public static readonly TimeSpan LateUploadWindow = TimeSpan.FromHours(48);
}

public sealed record LocationPointInput(DateTimeOffset RecordedAt, double Latitude, double Longitude,
    double? AccuracyMeters, double? SpeedMetersPerSecond, double? HeadingDegrees, bool IsMock, int? BatteryPercent,
    bool? IsCharging, bool? IsBatterySaverOn);

public sealed record RecordLocationPointsResultDto(int AttendanceSessionId, int Accepted, int Skipped, bool TrackingActive);

/// <summary>Everything the app needs to decide whether to ask for consent and start tracking.</summary>
public sealed record MyFieldTrackingStatusDto(bool TrackingEnabled, bool IsTrackedRole, bool MustTrack, int PolicyVersion,
    bool HasConsented, int? OpenSessionId, bool OpenSessionIsTracked, int MovingIntervalSeconds, int MovingDistanceMeters,
    int StillIntervalSeconds, int UploadIntervalSeconds, int MaxPointsPerUpload);

public sealed record FieldTrackingRoleDto(int OrganizationRoleId, string Name, bool IsTracked, int MemberCount);

public sealed record FieldTrackingSettingsDto(int OrganizationId, bool TrackingEnabled, int LocationRetentionDays,
    int PolicyVersion, bool RoadMatchingAvailable, IReadOnlyList<FieldTrackingRoleDto> Roles);

public sealed record LiveFieldMemberDto(int UserId, string FullName, string RoleName, int AttendanceSessionId,
    DateTime PunchInAtUtc, double? Latitude, double? Longitude, double? AccuracyMeters, DateTime? LastSeenAtUtc,
    int? BatteryPercent, bool? IsCharging, bool IsMock, bool FromPunchLocation);

public sealed record LiveFieldTrackingDto(DateTime GeneratedAtUtc, IReadOnlyList<LiveFieldMemberDto> Members);

public sealed record TrackedSegmentDto(int Sequence, TrackedSegmentKind Kind, DateTime StartedAtUtc, DateTime EndedAtUtc,
    string EncodedPolyline, double DistanceMeters, bool IsSnapped);

public sealed record TrackStopDto(double Latitude, double Longitude, DateTime ArrivedAtUtc, DateTime LeftAtUtc, double Minutes);

public sealed record TrackPointDto(DateTime RecordedAtUtc, double Latitude, double Longitude, double? AccuracyMeters, bool IsMock);

public sealed record BatterySampleDto(DateTime RecordedAtUtc, int BatteryPercent, bool? IsCharging);

public sealed record TrackedSessionDto(int AttendanceSessionId, DateTime PunchInAtUtc, DateTime? PunchOutAtUtc,
    AttendanceSessionStatus Status, bool IsTracked, double? PunchInLatitude, double? PunchInLongitude,
    double? PunchOutLatitude, double? PunchOutLongitude, IReadOnlyList<TrackedSegmentDto> Segments,
    IReadOnlyList<TrackStopDto> Stops,
    // Received but not yet folded into the matched route (the processing service runs every few minutes).
    IReadOnlyList<TrackPointDto> PendingPoints);

public sealed record FieldTrackingDayDto(int UserId, string FullName, DateOnly WorkDate, string TimeZone,
    double DistanceMeters, double InferredDistanceMeters, int StopCount, int MockPointCount, int PointCount,
    IReadOnlyList<BatterySampleDto> Battery, IReadOnlyList<TrackedSessionDto> Sessions);
