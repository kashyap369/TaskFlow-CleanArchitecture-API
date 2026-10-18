using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Domain.Entities.Organization;

/// <summary>
/// A role whose members are tracked while punched in (sales, field visits). Chosen by whoever
/// holds ManageAttendance. Roles are the organization's own, so this is a list of role ids,
/// never a role name.
/// </summary>
public sealed class AttendanceTrackedRole : AuditableEntity, IAggregateRoot
{
    public int OrganizationId { get; private set; }
    public int OrganizationRoleId { get; private set; }
    public int AddedByUserId { get; private set; }

    private AttendanceTrackedRole() { }

    public AttendanceTrackedRole(int organizationId, int organizationRoleId, int addedByUserId)
    {
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        if (organizationRoleId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationRoleId));
        if (addedByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(addedByUserId));
        OrganizationId = organizationId;
        OrganizationRoleId = organizationRoleId;
        AddedByUserId = addedByUserId;
    }
}

/// <summary>
/// A member's agreement to be tracked while punched in, for one version of the policy text.
/// Withdrawing it soft-deletes the row; a tracked member without consent cannot punch in.
/// </summary>
public sealed class FieldTrackingConsent : AuditableEntity, IAggregateRoot
{
    public int OrganizationId { get; private set; }
    public int UserId { get; private set; }
    public int PolicyVersion { get; private set; }
    public DateTime AcceptedAtUtc { get; private set; }

    private FieldTrackingConsent() { }

    public FieldTrackingConsent(int organizationId, int userId, int policyVersion, DateTime acceptedAtUtc)
    {
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        if (policyVersion <= 0) throw new ArgumentOutOfRangeException(nameof(policyVersion));
        OrganizationId = organizationId;
        UserId = userId;
        PolicyVersion = policyVersion;
        AcceptedAtUtc = DateTime.SpecifyKind(acceptedAtUtc, DateTimeKind.Utc);
    }

    public void Withdraw() => SoftDelete();
}

/// <summary>
/// One GPS fix from a tracked member's phone. Unlike almost everything else these are
/// <b>hard-deleted</b> after the organization's retention period: raw location is the most
/// sensitive data the system holds, and the matched route keeps what is worth keeping.
/// The time is the phone's — unavoidable, points are recorded offline — and is only accepted
/// inside the session's punch-in/punch-out window.
/// </summary>
public sealed class LocationPoint : BaseEntity, IAggregateRoot
{
    public int AttendanceSessionId { get; private set; }
    public int OrganizationId { get; private set; }
    public int UserId { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public double? AccuracyMeters { get; private set; }
    public double? SpeedMetersPerSecond { get; private set; }
    public double? HeadingDegrees { get; private set; }
    // The OS said the position came from a mock provider. A flag for the reviewer, not a block.
    public bool IsMock { get; private set; }
    public int? BatteryPercent { get; private set; }
    public bool? IsCharging { get; private set; }
    public bool? IsBatterySaverOn { get; private set; }
    // Folded into the session's matched route by the processing service.
    public bool IsProcessed { get; private set; }

    private LocationPoint() { }

    public LocationPoint(int attendanceSessionId, int organizationId, int userId, DateTime recordedAtUtc,
        DateTime receivedAtUtc, double latitude, double longitude, double? accuracyMeters, double? speedMetersPerSecond,
        double? headingDegrees, bool isMock, int? batteryPercent, bool? isCharging, bool? isBatterySaverOn)
    {
        if (attendanceSessionId <= 0) throw new ArgumentOutOfRangeException(nameof(attendanceSessionId));
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        if (double.IsNaN(latitude) || latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (double.IsNaN(longitude) || longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (batteryPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(batteryPercent));

        AttendanceSessionId = attendanceSessionId;
        OrganizationId = organizationId;
        UserId = userId;
        RecordedAtUtc = DateTime.SpecifyKind(recordedAtUtc, DateTimeKind.Utc);
        ReceivedAtUtc = DateTime.SpecifyKind(receivedAtUtc, DateTimeKind.Utc);
        Latitude = latitude;
        Longitude = longitude;
        AccuracyMeters = Finite(accuracyMeters);
        SpeedMetersPerSecond = Finite(speedMetersPerSecond);
        HeadingDegrees = Finite(headingDegrees);
        IsMock = isMock;
        BatteryPercent = batteryPercent;
        IsCharging = isCharging;
        IsBatterySaverOn = isBatterySaverOn;
    }

    public void MarkProcessed() => IsProcessed = true;

    private static double? Finite(double? value) => value is { } x && double.IsFinite(x) && x >= 0 ? x : null;
}

/// <summary>
/// A piece of a tracked session's route, as an encoded polyline (precision 6, the format
/// Valhalla returns). Rebuilt by the processing service while the session has new points.
/// </summary>
public sealed class TrackedRouteSegment : BaseEntity, IAggregateRoot
{
    public int AttendanceSessionId { get; private set; }
    public int OrganizationId { get; private set; }
    public int UserId { get; private set; }
    public int Sequence { get; private set; }
    public TrackedSegmentKind Kind { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime EndedAtUtc { get; private set; }
    public string EncodedPolyline { get; private set; } = string.Empty;
    public double DistanceMeters { get; private set; }
    public int PointCount { get; private set; }
    // False when the road matcher was unavailable or failed and the raw (cleaned) track was kept.
    public bool IsSnapped { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private TrackedRouteSegment() { }

    public TrackedRouteSegment(int attendanceSessionId, int organizationId, int userId, int sequence,
        TrackedSegmentKind kind, DateTime startedAtUtc, DateTime endedAtUtc, string encodedPolyline,
        double distanceMeters, int pointCount, bool isSnapped, DateTime createdAtUtc)
    {
        if (attendanceSessionId <= 0) throw new ArgumentOutOfRangeException(nameof(attendanceSessionId));
        if (string.IsNullOrEmpty(encodedPolyline)) throw new ArgumentException("A segment needs a shape.", nameof(encodedPolyline));
        if (endedAtUtc < startedAtUtc) throw new ArgumentException("A segment cannot end before it starts.", nameof(endedAtUtc));
        if (double.IsNaN(distanceMeters) || distanceMeters < 0) throw new ArgumentOutOfRangeException(nameof(distanceMeters));

        AttendanceSessionId = attendanceSessionId;
        OrganizationId = organizationId;
        UserId = userId;
        Sequence = sequence;
        Kind = kind;
        StartedAtUtc = DateTime.SpecifyKind(startedAtUtc, DateTimeKind.Utc);
        EndedAtUtc = DateTime.SpecifyKind(endedAtUtc, DateTimeKind.Utc);
        EncodedPolyline = encodedPolyline;
        DistanceMeters = distanceMeters;
        PointCount = pointCount;
        IsSnapped = isSnapped;
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
    }
}

/// <summary>A place the member stayed (within ~100 m) for at least ten minutes.</summary>
public sealed class TrackStop : BaseEntity, IAggregateRoot
{
    public int AttendanceSessionId { get; private set; }
    public int OrganizationId { get; private set; }
    public int UserId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public DateTime ArrivedAtUtc { get; private set; }
    public DateTime LeftAtUtc { get; private set; }

    private TrackStop() { }

    public TrackStop(int attendanceSessionId, int organizationId, int userId, double latitude, double longitude,
        DateTime arrivedAtUtc, DateTime leftAtUtc)
    {
        if (attendanceSessionId <= 0) throw new ArgumentOutOfRangeException(nameof(attendanceSessionId));
        if (leftAtUtc < arrivedAtUtc) throw new ArgumentException("A stop cannot end before it starts.", nameof(leftAtUtc));
        AttendanceSessionId = attendanceSessionId;
        OrganizationId = organizationId;
        UserId = userId;
        Latitude = latitude;
        Longitude = longitude;
        ArrivedAtUtc = DateTime.SpecifyKind(arrivedAtUtc, DateTimeKind.Utc);
        LeftAtUtc = DateTime.SpecifyKind(leftAtUtc, DateTimeKind.Utc);
    }
}
