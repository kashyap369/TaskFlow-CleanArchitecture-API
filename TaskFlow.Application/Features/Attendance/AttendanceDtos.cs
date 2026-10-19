using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Attendance;

public sealed record AttendanceSettingsDto(int OrganizationId, string TimeZone, int AutoCloseAfterHours,
    int PhotoRetentionDays, bool SelfieRequired);

public sealed record AttendanceSessionDto(int Id, int UserId, DateOnly WorkDate, DateTime PunchInAtUtc,
    DateTime? PunchOutAtUtc, AttendanceSessionStatus Status, string? PunchInNote, string? PunchOutNote,
    bool HasPunchInPhoto, bool HasPunchOutPhoto, double WorkedMinutes, string? AdjustmentReason,
    DateTime? OriginalPunchInAtUtc, DateTime? OriginalPunchOutAtUtc);

public sealed record AttendanceTodayDto(DateOnly WorkDate, string TimeZone, bool IsPunchedIn, int? OpenSessionId,
    DateTime? OpenSinceUtc, double WorkedMinutes, bool SelfieRequired, IReadOnlyList<AttendanceSessionDto> Sessions);

public enum AttendanceDayStatus
{
    // No punch and no leave or holiday. Not called "absent": there are no work schedules yet,
    // so a weekend looks exactly the same.
    NoRecord = 0,
    Present = 1,
    // Still open, or closed by the system because the punch-out was forgotten.
    Incomplete = 2,
    OnLeave = 3,
    Holiday = 4
}

public sealed record AttendanceDayDto(DateOnly WorkDate, AttendanceDayStatus Status, DateTime? FirstPunchInUtc,
    DateTime? LastPunchOutUtc, double WorkedMinutes, string? CalendarNote, IReadOnlyList<AttendanceSessionDto> Sessions);

public enum AttendanceRosterStatus
{
    In = 1,
    Out = 2,
    NotIn = 3,
    OnLeave = 4,
    Holiday = 5
}

public sealed record AttendanceRosterRowDto(int UserId, string FullName, string Email, string RoleName,
    AttendanceRosterStatus Status, DateTime? FirstPunchInUtc, DateTime? LastPunchOutUtc, double WorkedMinutes,
    int? OpenSessionId);

public sealed record AttendanceRosterDto(DateOnly WorkDate, string TimeZone, IReadOnlyList<AttendanceRosterRowDto> Members);

public sealed record AttendancePhotoFile(byte[] Content, string ContentType, string FileName);

/// <summary>
/// One member who has at least one attendance session on a work date. Feeds the member pickers on
/// the map tabs, which must offer only people who actually punched in that day.
/// </summary>
public sealed record AttendedMemberDto(int UserId, string FullName, string RoleName,
    int SessionCount, DateTime FirstPunchInUtc, DateTime? LastPunchOutUtc, bool IsStillIn, bool WasTracked);

public sealed record AttendedMembersDto(DateOnly WorkDate, string TimeZone, IReadOnlyList<AttendedMemberDto> Members);
