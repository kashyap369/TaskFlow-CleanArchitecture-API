using TaskFlow.Domain.Common;
using TaskFlow.Domain.DomainEvents.Organizations;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Domain.Entities.Organization;

/// <summary>
/// The selfie taken at a punch. The image itself lives in object storage; the row keeps the
/// pointer and a fingerprint, the same way <c>PlannerAsset</c> does.
/// </summary>
public sealed record AttendancePhoto(string StorageKey, string ContentType, long SizeBytes, string Sha256);

/// <summary>
/// One stretch of being at work: a punch-in and, later, a punch-out. A member may have several
/// on one work date (breaks, split shifts). Punch times always come from the server clock.
/// At most one session per member per organization is open — enforced by a filtered unique
/// index, not only here, so two taps racing from a phone cannot both open one.
/// </summary>
public sealed class AttendanceSession : AuditableEntity, IAggregateRoot
{
    public int OrganizationId { get; private set; }
    public int UserId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public DateTime PunchInAtUtc { get; private set; }
    public DateTime? PunchOutAtUtc { get; private set; }
    public AttendanceSessionStatus Status { get; private set; }
    public string? PunchInNote { get; private set; }
    public string? PunchOutNote { get; private set; }
    public string? PunchInPhotoKey { get; private set; }
    public string? PunchInPhotoContentType { get; private set; }
    public long? PunchInPhotoSizeBytes { get; private set; }
    public string? PunchInPhotoSha256 { get; private set; }
    public string? PunchOutPhotoKey { get; private set; }
    public string? PunchOutPhotoContentType { get; private set; }
    public long? PunchOutPhotoSizeBytes { get; private set; }
    public string? PunchOutPhotoSha256 { get; private set; }
    public DateTime? PhotosPurgedAtUtc { get; private set; }
    public DateTime? OriginalPunchInAtUtc { get; private set; }
    public DateTime? OriginalPunchOutAtUtc { get; private set; }
    public int? AdjustedByUserId { get; private set; }
    public string? AdjustmentReason { get; private set; }
    public DateTime? AdjustedAtUtc { get; private set; }

    public bool IsOpen => Status == AttendanceSessionStatus.Open;

    private AttendanceSession() { }

    private AttendanceSession(int organizationId, int userId, DateOnly workDate, DateTime punchInAtUtc,
        string? note, AttendancePhoto photo)
    {
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        ArgumentNullException.ThrowIfNull(photo);

        OrganizationId = organizationId;
        UserId = userId;
        WorkDate = workDate;
        PunchInAtUtc = DateTime.SpecifyKind(punchInAtUtc, DateTimeKind.Utc);
        Status = AttendanceSessionStatus.Open;
        PunchInNote = Clean(note);
        PunchInPhotoKey = photo.StorageKey;
        PunchInPhotoContentType = photo.ContentType;
        PunchInPhotoSizeBytes = photo.SizeBytes;
        PunchInPhotoSha256 = photo.Sha256;
        AddDomainEvent(new AttendancePunchedInEvent(organizationId, userId, PunchInAtUtc));
    }

    public static AttendanceSession PunchIn(int organizationId, int userId, DateOnly workDate, DateTime nowUtc,
        string? note, AttendancePhoto photo) =>
        new(organizationId, userId, workDate, nowUtc, note, photo);

    public void PunchOut(DateTime nowUtc, string? note, AttendancePhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);
        if (!IsOpen) throw new InvalidOperationException("Only an open attendance session can be punched out.");
        var punchOut = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (punchOut < PunchInAtUtc) throw new ArgumentException("Punch-out cannot be before punch-in.", nameof(nowUtc));

        PunchOutAtUtc = punchOut;
        Status = AttendanceSessionStatus.Closed;
        PunchOutNote = Clean(note);
        PunchOutPhotoKey = photo.StorageKey;
        PunchOutPhotoContentType = photo.ContentType;
        PunchOutPhotoSizeBytes = photo.SizeBytes;
        PunchOutPhotoSha256 = photo.Sha256;
        MarkAsUpdated();
        AddDomainEvent(new AttendancePunchedOutEvent(OrganizationId, UserId, punchOut));
    }

    /// <summary>
    /// Closes a session whose punch-out was forgotten, so it cannot block the next punch-in.
    /// The closing time is the system's, not the member's: the session counts as incomplete,
    /// not as time worked, until someone with ManageAttendance adjusts it.
    /// </summary>
    public void AutoClose(DateTime closedAtUtc)
    {
        if (!IsOpen) return;
        var closedAt = DateTime.SpecifyKind(closedAtUtc, DateTimeKind.Utc);
        PunchOutAtUtc = closedAt < PunchInAtUtc ? PunchInAtUtc : closedAt;
        Status = AttendanceSessionStatus.AutoClosed;
        MarkAsUpdated();
    }

    /// <summary>
    /// Corrects the times. The first adjustment keeps the original times; later ones do not
    /// overwrite them, so the record always shows what was actually punched.
    /// </summary>
    public void Adjust(DateTime punchInAtUtc, DateTime punchOutAtUtc, string reason, int adjustedByUserId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        if (adjustedByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adjustedByUserId));
        var punchIn = DateTime.SpecifyKind(punchInAtUtc, DateTimeKind.Utc);
        var punchOut = DateTime.SpecifyKind(punchOutAtUtc, DateTimeKind.Utc);
        if (punchOut <= punchIn) throw new ArgumentException("Punch-out must be after punch-in.", nameof(punchOutAtUtc));

        if (AdjustedByUserId is null)
        {
            OriginalPunchInAtUtc = PunchInAtUtc;
            OriginalPunchOutAtUtc = PunchOutAtUtc;
        }

        PunchInAtUtc = punchIn;
        PunchOutAtUtc = punchOut;
        Status = AttendanceSessionStatus.Adjusted;
        AdjustedByUserId = adjustedByUserId;
        AdjustmentReason = reason.Trim();
        AdjustedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        MarkAsUpdated();
        AddDomainEvent(new AttendanceAdjustedEvent(OrganizationId, UserId, Id, adjustedByUserId));
    }

    /// <summary>
    /// Forgets the selfies after the retention window. The caller deletes the objects first;
    /// the attendance record itself stays.
    /// </summary>
    public void PurgePhotos(DateTime nowUtc)
    {
        PunchInPhotoKey = null;
        PunchInPhotoContentType = null;
        PunchInPhotoSizeBytes = null;
        PunchInPhotoSha256 = null;
        PunchOutPhotoKey = null;
        PunchOutPhotoContentType = null;
        PunchOutPhotoSizeBytes = null;
        PunchOutPhotoSha256 = null;
        PhotosPurgedAtUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        MarkAsUpdated();
    }

    private static string? Clean(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
