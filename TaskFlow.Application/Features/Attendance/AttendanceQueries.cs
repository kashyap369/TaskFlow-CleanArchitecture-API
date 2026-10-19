using System.Data;
using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Calendar;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Attendance;

public sealed record GetAttendanceSettingsQuery(int OrganizationId)
    : IRequest<AttendanceSettingsDto>, IOrganizationScopedRequest;

public sealed record GetMyAttendanceTodayQuery(int OrganizationId)
    : IRequest<AttendanceTodayDto>, IOrganizationScopedRequest;

public sealed record GetMyAttendanceQuery(int OrganizationId, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<AttendanceDayDto>>, IOrganizationScopedRequest;

/// <summary>Another member's history: ViewAttendance, checked in the handler (membership alone is not enough).</summary>
public sealed record GetMemberAttendanceQuery(int OrganizationId, int UserId, DateOnly From, DateOnly To)
    : IRequest<IReadOnlyList<AttendanceDayDto>>, IOrganizationScopedRequest;

/// <summary>Everyone's status for one day: ViewAttendance, checked in the handler.</summary>
public sealed record GetAttendanceRosterQuery(int OrganizationId, DateOnly? Date, int? TeamId)
    : IRequest<AttendanceRosterDto>, IOrganizationScopedRequest;

/// <summary>A punch selfie: the member's own, or anyone's in the organization with ViewAttendance.</summary>
public sealed record GetAttendancePhotoQuery(int SessionId, string Kind) : IRequest<AttendancePhotoFile>;

/// <summary>
/// Who actually punched in on one work date. The member pickers on the attendance map tabs are
/// built from this, not from the whole member list: picking someone who was never in that day
/// only ever produces an empty route. Names and roles only — no location and no contact detail.
/// ViewAttendance or ViewFieldTracking, checked in the handler.
/// </summary>
public sealed record GetAttendedMembersQuery(int OrganizationId, DateOnly? Date)
    : IRequest<AttendedMembersDto>, IOrganizationScopedRequest;

public sealed class GetMyAttendanceQueryValidator : AbstractValidator<GetMyAttendanceQuery>
{
    public GetMyAttendanceQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.To >= x.From).WithMessage("To must not be before from.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 62).WithMessage("Attendance history is limited to 62 days at a time.");
    }
}

public sealed class GetMemberAttendanceQueryValidator : AbstractValidator<GetMemberAttendanceQuery>
{
    public GetMemberAttendanceQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.To >= x.From).WithMessage("To must not be before from.");
        RuleFor(x => x).Must(x => x.To.DayNumber - x.From.DayNumber <= 62).WithMessage("Attendance history is limited to 62 days at a time.");
    }
}

public sealed class GetAttendancePhotoQueryValidator : AbstractValidator<GetAttendancePhotoQuery>
{
    public GetAttendancePhotoQueryValidator()
    {
        RuleFor(x => x.SessionId).GreaterThan(0);
        RuleFor(x => x.Kind).Must(x => x is "in" or "out").WithMessage("Photo kind must be 'in' or 'out'.");
    }
}

public sealed class GetAttendanceSettingsQueryHandler : IRequestHandler<GetAttendanceSettingsQuery, AttendanceSettingsDto>
{
    private readonly ISqlConnectionFactory _sql;
    public GetAttendanceSettingsQueryHandler(ISqlConnectionFactory sql) => _sql = sql;
    public async Task<AttendanceSettingsDto> Handle(GetAttendanceSettingsQuery request, CancellationToken cancellationToken)
    {
        using var connection = _sql.Create();
        return await AttendanceReads.LoadSettingsAsync(connection, request.OrganizationId, cancellationToken);
    }
}

public sealed class GetMyAttendanceTodayQueryHandler : IRequestHandler<GetMyAttendanceTodayQuery, AttendanceTodayDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    public GetMyAttendanceTodayQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user) => (_sql, _user) = (sql, user);
    public async Task<AttendanceTodayDto> Handle(GetMyAttendanceTodayQuery request, CancellationToken cancellationToken)
    {
        using var connection = _sql.Create();
        var settings = await AttendanceReads.LoadSettingsAsync(connection, request.OrganizationId, cancellationToken);
        var now = DateTime.UtcNow;
        var today = AttendanceClock.WorkDate(now, AttendanceClock.Resolve(settings.TimeZone));
        var rows = await AttendanceReads.LoadSessionsAsync(connection, request.OrganizationId, _user.UserId, today, today, cancellationToken);
        // An open session may have started on an earlier work date; it still means "in" now.
        var open = await AttendanceReads.LoadOpenSessionAsync(connection, request.OrganizationId, _user.UserId, cancellationToken);
        if (open is not null && rows.All(x => x.Id != open.Id)) rows.Add(open);
        var sessions = rows.OrderBy(x => x.PunchInAtUtc).Select(x => AttendanceReads.ToDto(x, now)).ToList();
        return new AttendanceTodayDto(today, settings.TimeZone, open is not null, open?.Id,
            open is null ? null : AttendanceReads.Utc(open.PunchInAtUtc), sessions.Sum(x => x.WorkedMinutes),
            settings.SelfieRequired, sessions);
    }
}

public sealed class GetMyAttendanceQueryHandler : IRequestHandler<GetMyAttendanceQuery, IReadOnlyList<AttendanceDayDto>>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user; private readonly ISender _sender;
    public GetMyAttendanceQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user, ISender sender) =>
        (_sql, _user, _sender) = (sql, user, sender);
    public Task<IReadOnlyList<AttendanceDayDto>> Handle(GetMyAttendanceQuery request, CancellationToken cancellationToken) =>
        AttendanceReads.LoadDaysAsync(_sql, _sender, request.OrganizationId, _user.UserId, request.From, request.To, cancellationToken);
}

public sealed class GetMemberAttendanceQueryHandler : IRequestHandler<GetMemberAttendanceQuery, IReadOnlyList<AttendanceDayDto>>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions; private readonly ISender _sender;
    public GetMemberAttendanceQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions, ISender sender) =>
        (_sql, _user, _permissions, _sender) = (sql, user, permissions, sender);
    public async Task<IReadOnlyList<AttendanceDayDto>> Handle(GetMemberAttendanceQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId != _user.UserId)
            await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
                OrganizationPermissionNames.ViewAttendance, cancellationToken);
        return await AttendanceReads.LoadDaysAsync(_sql, _sender, request.OrganizationId, request.UserId,
            request.From, request.To, cancellationToken);
    }
}

public sealed class GetAttendanceRosterQueryHandler : IRequestHandler<GetAttendanceRosterQuery, AttendanceRosterDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions; private readonly ISender _sender;
    public GetAttendanceRosterQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions, ISender sender) =>
        (_sql, _user, _permissions, _sender) = (sql, user, permissions, sender);
    public async Task<AttendanceRosterDto> Handle(GetAttendanceRosterQuery request, CancellationToken cancellationToken)
    {
        await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ViewAttendance, cancellationToken);

        // The owner has no member row, so is added when not already listed (and only for the whole organization).
        const string membersSql = """
            SELECT m."UserId", u."FirstName" || ' ' || u."LastName" AS "FullName", u."Email", r."Name" AS "RoleName"
            FROM "OrganizationMembers" m
            JOIN "Users" u ON u."Id" = m."UserId"
            JOIN "OrganizationRoles" r ON r."Id" = m."OrganizationRoleId"
            WHERE m."OrganizationId" = @OrganizationId AND m."IsActive" = TRUE AND m."IsDeleted" = FALSE
              AND (@TeamId IS NULL OR EXISTS (
                    SELECT 1 FROM "TeamMembers" tm
                    WHERE tm."TeamId" = @TeamId AND tm."UserId" = m."UserId"
                      AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE))
            UNION
            SELECT o."OwnerUserId", u."FirstName" || ' ' || u."LastName", u."Email", 'Owner'
            FROM "Organizations" o
            JOIN "Users" u ON u."Id" = o."OwnerUserId"
            WHERE o."Id" = @OrganizationId AND o."IsDeleted" = FALSE AND @TeamId IS NULL
              AND NOT EXISTS (
                    SELECT 1 FROM "OrganizationMembers" m
                    WHERE m."OrganizationId" = o."Id" AND m."UserId" = o."OwnerUserId"
                      AND m."IsActive" = TRUE AND m."IsDeleted" = FALSE);
            """;

        using var connection = _sql.Create();
        var settings = await AttendanceReads.LoadSettingsAsync(connection, request.OrganizationId, cancellationToken);
        var zone = AttendanceClock.Resolve(settings.TimeZone);
        var now = DateTime.UtcNow;
        var date = request.Date ?? AttendanceClock.WorkDate(now, zone);
        var members = (await connection.QueryAsync<RosterMemberRow>(new CommandDefinition(membersSql,
            new { request.OrganizationId, request.TeamId }, cancellationToken: cancellationToken))).ToList();
        var sessions = await AttendanceReads.LoadSessionsAsync(connection, request.OrganizationId, null, date, date, cancellationToken);
        var calendar = await AttendanceReads.LoadCalendarAsync(_sender, request.OrganizationId, date, date, cancellationToken);

        var rows = members.Select(member =>
        {
            var mine = sessions.Where(x => x.UserId == member.UserId).OrderBy(x => x.PunchInAtUtc).ToList();
            var open = mine.FirstOrDefault(x => x.Status == AttendanceSessionStatus.Open);
            var status = open is not null ? AttendanceRosterStatus.In
                : mine.Count > 0 ? AttendanceRosterStatus.Out
                : calendar.LeaveTitle(member.UserId, date) is not null ? AttendanceRosterStatus.OnLeave
                : calendar.HolidayTitle(date) is not null ? AttendanceRosterStatus.Holiday
                : AttendanceRosterStatus.NotIn;
            return new AttendanceRosterRowDto(member.UserId, member.FullName, member.Email, member.RoleName, status,
                mine.Count == 0 ? null : AttendanceReads.Utc(mine[0].PunchInAtUtc),
                mine.Where(x => x.PunchOutAtUtc is not null).Select(x => (DateTime?)AttendanceReads.Utc(x.PunchOutAtUtc!.Value)).Max(),
                mine.Sum(x => AttendanceReads.WorkedMinutes(x, now)), open?.Id);
        })
        .OrderBy(x => x.Status).ThenBy(x => x.FullName)
        .ToList();

        return new AttendanceRosterDto(date, settings.TimeZone, rows);
    }

    public sealed class RosterMemberRow
    {
        public int UserId { get; init; } public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty; public string RoleName { get; init; } = string.Empty;
    }
}

public sealed class GetAttendancePhotoQueryHandler : IRequestHandler<GetAttendancePhotoQuery, AttendancePhotoFile>
{
    private readonly ISqlConnectionFactory _sql; private readonly IOrganizationAccessGuard _access;
    private readonly IOrganizationPermissionChecker _permissions; private readonly ICurrentUserService _user;
    private readonly IObjectStorage _storage;
    public GetAttendancePhotoQueryHandler(ISqlConnectionFactory sql, IOrganizationAccessGuard access,
        IOrganizationPermissionChecker permissions, ICurrentUserService user, IObjectStorage storage) =>
        (_sql, _access, _permissions, _user, _storage) = (sql, access, permissions, user, storage);
    public async Task<AttendancePhotoFile> Handle(GetAttendancePhotoQuery request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s."OrganizationId", s."UserId", s."PunchInPhotoKey", s."PunchInPhotoContentType",
                   s."PunchOutPhotoKey", s."PunchOutPhotoContentType"
            FROM "AttendanceSessions" s
            WHERE s."Id" = @SessionId AND s."IsDeleted" = FALSE;
            """;
        using var connection = _sql.Create();
        var row = await connection.QuerySingleOrDefaultAsync<PhotoRow>(new CommandDefinition(sql,
            new { request.SessionId }, cancellationToken: cancellationToken)) ??
            throw new NotFoundException("ATTENDANCE_SESSION_NOT_FOUND", "Attendance record not found.");

        await _access.EnsureOrganizationAsync(row.OrganizationId, cancellationToken);
        if (row.UserId != _user.UserId)
            await _permissions.EnsurePermissionAsync(row.OrganizationId, _user.UserId,
                OrganizationPermissionNames.ViewAttendance, cancellationToken);

        var (key, contentType) = request.Kind == "in"
            ? (row.PunchInPhotoKey, row.PunchInPhotoContentType)
            : (row.PunchOutPhotoKey, row.PunchOutPhotoContentType);
        if (string.IsNullOrEmpty(key))
            throw new NotFoundException("ATTENDANCE_PHOTO_NOT_FOUND", "There is no selfie for this punch, or it has passed its retention period.");

        var stored = await _storage.DownloadAsync(key, cancellationToken);
        var extension = contentType switch { "image/png" => ".png", "image/heic" => ".heic", _ => ".jpg" };
        return new AttendancePhotoFile(stored.Content, contentType ?? stored.ContentType,
            $"attendance-{request.SessionId}-{request.Kind}{extension}");
    }

    public sealed class PhotoRow
    {
        public int OrganizationId { get; init; } public int UserId { get; init; }
        public string? PunchInPhotoKey { get; init; } public string? PunchInPhotoContentType { get; init; }
        public string? PunchOutPhotoKey { get; init; } public string? PunchOutPhotoContentType { get; init; }
    }
}

public sealed class GetAttendedMembersQueryHandler : IRequestHandler<GetAttendedMembersQuery, AttendedMembersDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions;
    public GetAttendedMembersQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions) => (_sql, _user, _permissions) = (sql, user, permissions);
    public async Task<AttendedMembersDto> Handle(GetAttendedMembersQuery request, CancellationToken cancellationToken)
    {
        // Either permission is enough: this is the picker for both the attendance tabs and the map
        // tabs, and it reveals no more than "this person was at work that day".
        if (!await _permissions.HasPermissionAsync(request.OrganizationId, _user.UserId,
                OrganizationPermissionNames.ViewAttendance, cancellationToken) &&
            !await _permissions.HasPermissionAsync(request.OrganizationId, _user.UserId,
                OrganizationPermissionNames.ViewFieldTracking, cancellationToken))
            throw new ForbiddenException("PERMISSION_DENIED",
                "Listing who punched in needs the ViewAttendance or ViewFieldTracking permission.");

        // The owner has no member row, so the role falls back to "Owner" exactly as the live map does.
        const string sql = """
            SELECT s."UserId", u."FirstName" || ' ' || u."LastName" AS "FullName",
                   COALESCE(r."Name", 'Owner') AS "RoleName",
                   COUNT(*)::int AS "SessionCount",
                   MIN(s."PunchInAtUtc") AS "FirstPunchInUtc",
                   MAX(s."PunchOutAtUtc") AS "LastPunchOutUtc",
                   BOOL_OR(s."Status" = @Open) AS "IsStillIn",
                   BOOL_OR(s."IsTracked") AS "WasTracked"
            FROM "AttendanceSessions" s
            JOIN "Users" u ON u."Id" = s."UserId" AND u."IsDeleted" = FALSE
            LEFT JOIN "OrganizationMembers" m ON m."OrganizationId" = s."OrganizationId"
                 AND m."UserId" = s."UserId" AND m."IsDeleted" = FALSE
            LEFT JOIN "OrganizationRoles" r ON r."Id" = m."OrganizationRoleId" AND r."IsDeleted" = FALSE
            WHERE s."OrganizationId" = @OrganizationId AND s."WorkDate" = @Date::date AND s."IsDeleted" = FALSE
            GROUP BY s."UserId", u."FirstName", u."LastName", r."Name"
            ORDER BY u."FirstName", u."LastName";
            """;

        using var connection = _sql.Create();
        var settings = await AttendanceReads.LoadSettingsAsync(connection, request.OrganizationId, cancellationToken);
        var date = request.Date ?? AttendanceClock.WorkDate(DateTime.UtcNow, AttendanceClock.Resolve(settings.TimeZone));
        var rows = await connection.QueryAsync<AttendedRow>(new CommandDefinition(sql, new
        {
            request.OrganizationId,
            Date = date.ToDateTime(TimeOnly.MinValue),
            Open = (int)AttendanceSessionStatus.Open
        }, cancellationToken: cancellationToken));
        return new AttendedMembersDto(date, settings.TimeZone, rows
            .Select(x => new AttendedMemberDto(x.UserId, x.FullName, x.RoleName, x.SessionCount,
                AttendanceReads.Utc(x.FirstPunchInUtc),
                x.LastPunchOutUtc is { } end ? AttendanceReads.Utc(end) : null, x.IsStillIn, x.WasTracked))
            .ToList());
    }

    public sealed class AttendedRow
    {
        public int UserId { get; init; } public string FullName { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty; public int SessionCount { get; init; }
        public DateTime FirstPunchInUtc { get; init; } public DateTime? LastPunchOutUtc { get; init; }
        public bool IsStillIn { get; init; } public bool WasTracked { get; init; }
    }
}

/// <summary>Reads shared by the attendance queries. Raw SQL, quoted identifiers, soft-delete filtered.</summary>
internal static class AttendanceReads
{
    public static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Closed and adjusted sessions count; an open one counts up to now; an auto-closed one counts
    /// nothing — its end is the system's guess, not the member's.
    /// </summary>
    public static double WorkedMinutes(AttendanceSessionRow row, DateTime nowUtc) => row.Status switch
    {
        AttendanceSessionStatus.Closed or AttendanceSessionStatus.Adjusted when row.PunchOutAtUtc is { } end =>
            Math.Max(0, (Utc(end) - Utc(row.PunchInAtUtc)).TotalMinutes),
        AttendanceSessionStatus.Open => Math.Max(0, (nowUtc - Utc(row.PunchInAtUtc)).TotalMinutes),
        _ => 0
    };

    public static AttendanceSessionDto ToDto(AttendanceSessionRow row, DateTime nowUtc) =>
        new(row.Id, row.UserId, row.WorkDate, Utc(row.PunchInAtUtc),
            row.PunchOutAtUtc is { } end ? Utc(end) : null, row.Status, row.PunchInNote, row.PunchOutNote,
            !string.IsNullOrEmpty(row.PunchInPhotoKey), !string.IsNullOrEmpty(row.PunchOutPhotoKey),
            WorkedMinutes(row, nowUtc), row.AdjustmentReason,
            row.OriginalPunchInAtUtc is { } originalIn ? Utc(originalIn) : null,
            row.OriginalPunchOutAtUtc is { } originalOut ? Utc(originalOut) : null);

    public static async Task<AttendanceSettingsDto> LoadSettingsAsync(IDbConnection connection, int organizationId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT s."TimeZone", s."AutoCloseAfterHours", s."PhotoRetentionDays"
            FROM "AttendanceSettings" s
            WHERE s."OrganizationId" = @OrganizationId AND s."IsDeleted" = FALSE;
            """;
        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(new CommandDefinition(sql,
            new { OrganizationId = organizationId }, cancellationToken: cancellationToken));
        return new AttendanceSettingsDto(organizationId,
            row?.TimeZone ?? AttendanceSettings.DefaultTimeZone,
            row?.AutoCloseAfterHours ?? AttendanceSettings.DefaultAutoCloseAfterHours,
            row?.PhotoRetentionDays ?? AttendanceSettings.DefaultPhotoRetentionDays,
            SelfieRequired: true);
    }

    private const string SessionColumns = """
        s."Id", s."OrganizationId", s."UserId", s."WorkDate", s."PunchInAtUtc", s."PunchOutAtUtc", s."Status",
        s."PunchInNote", s."PunchOutNote", s."PunchInPhotoKey", s."PunchOutPhotoKey", s."AdjustmentReason",
        s."OriginalPunchInAtUtc", s."OriginalPunchOutAtUtc"
        """;

    /// <param name="userId">One member, or null for everyone in the organization.</param>
    public static async Task<List<AttendanceSessionRow>> LoadSessionsAsync(IDbConnection connection, int organizationId,
        int? userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SessionColumns}
            FROM "AttendanceSessions" s
            WHERE s."OrganizationId" = @OrganizationId AND s."IsDeleted" = FALSE
              AND (@UserId IS NULL OR s."UserId" = @UserId)
              AND s."WorkDate" >= @From::date AND s."WorkDate" <= @To::date
            ORDER BY s."PunchInAtUtc", s."Id";
            """;
        var rows = await connection.QueryAsync<AttendanceSessionRow>(new CommandDefinition(sql, new
        {
            OrganizationId = organizationId,
            UserId = userId,
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MinValue)
        }, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public static Task<AttendanceSessionRow?> LoadOpenSessionAsync(IDbConnection connection, int organizationId,
        int userId, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {SessionColumns}
            FROM "AttendanceSessions" s
            WHERE s."OrganizationId" = @OrganizationId AND s."UserId" = @UserId
              AND s."Status" = @Open AND s."IsDeleted" = FALSE;
            """;
        return connection.QuerySingleOrDefaultAsync<AttendanceSessionRow?>(new CommandDefinition(sql, new
        {
            OrganizationId = organizationId,
            UserId = userId,
            Open = (int)AttendanceSessionStatus.Open
        }, cancellationToken: cancellationToken));
    }

    /// <summary>Day by day, newest first, up to today in the organization's time zone.</summary>
    public static async Task<IReadOnlyList<AttendanceDayDto>> LoadDaysAsync(ISqlConnectionFactory sql, ISender sender,
        int organizationId, int userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        using var connection = sql.Create();
        var settings = await LoadSettingsAsync(connection, organizationId, cancellationToken);
        var now = DateTime.UtcNow;
        var today = AttendanceClock.WorkDate(now, AttendanceClock.Resolve(settings.TimeZone));
        if (to > today) to = today;
        if (from > to) return Array.Empty<AttendanceDayDto>();

        var sessions = await LoadSessionsAsync(connection, organizationId, userId, from, to, cancellationToken);
        var calendar = await LoadCalendarAsync(sender, organizationId, from, to, cancellationToken);
        var days = new List<AttendanceDayDto>();
        for (var date = to; date >= from; date = date.AddDays(-1))
        {
            var day = date;
            var mine = sessions.Where(x => x.WorkDate == day).OrderBy(x => x.PunchInAtUtc).ToList();
            var leave = calendar.LeaveTitle(userId, day);
            var holiday = calendar.HolidayTitle(day);
            var status = mine.Count > 0
                ? mine.Any(x => x.Status is AttendanceSessionStatus.Open or AttendanceSessionStatus.AutoClosed)
                    ? AttendanceDayStatus.Incomplete
                    : AttendanceDayStatus.Present
                : leave is not null ? AttendanceDayStatus.OnLeave
                : holiday is not null ? AttendanceDayStatus.Holiday
                : AttendanceDayStatus.NoRecord;
            days.Add(new AttendanceDayDto(day, status,
                mine.Count == 0 ? null : Utc(mine[0].PunchInAtUtc),
                mine.Where(x => x.PunchOutAtUtc is not null).Select(x => (DateTime?)Utc(x.PunchOutAtUtc!.Value)).Max(),
                mine.Sum(x => WorkedMinutes(x, now)), leave ?? holiday,
                mine.Select(x => ToDto(x, now)).ToList()));
        }
        return days;
    }

    /// <summary>
    /// Leave and holidays come from the existing calendar query — its recurrence expansion is not
    /// repeated here. All-day entries span UTC midnights, so their days are read as UTC dates.
    /// </summary>
    public static async Task<CalendarDays> LoadCalendarAsync(ISender sender, int organizationId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var entries = await sender.Send(new GetCalendarEntriesQuery(organizationId, fromUtc, toUtc), cancellationToken);
        return new CalendarDays(entries
            .Where(x => x.Kind is CalendarEntryKind.Holiday or CalendarEntryKind.MemberLeave)
            .ToList());
    }

    public sealed class CalendarDays
    {
        private readonly IReadOnlyList<CalendarEntryDto> _entries;
        public CalendarDays(IReadOnlyList<CalendarEntryDto> entries) => _entries = entries;

        public string? HolidayTitle(DateOnly date) => _entries
            .FirstOrDefault(x => x.Kind == CalendarEntryKind.Holiday && Covers(x, date))?.Title;

        public string? LeaveTitle(int userId, DateOnly date) => _entries
            .FirstOrDefault(x => x.Kind == CalendarEntryKind.MemberLeave && x.MemberUserId == userId && Covers(x, date))?.Title;

        private static bool Covers(CalendarEntryDto entry, DateOnly date) =>
            DateOnly.FromDateTime(entry.StartsAtUtc) <= date && date < DateOnly.FromDateTime(entry.EndsAtUtc);
    }

    public sealed class SettingsRow
    {
        public string TimeZone { get; init; } = AttendanceSettings.DefaultTimeZone;
        public int AutoCloseAfterHours { get; init; } public int PhotoRetentionDays { get; init; }
    }
}

public sealed class AttendanceSessionRow
{
    public int Id { get; init; } public int OrganizationId { get; init; } public int UserId { get; init; }
    // A "date" column read straight into DateOnly, as CalendarEntryRow.RecurrenceUntil is.
    public DateOnly WorkDate { get; init; }
    public DateTime PunchInAtUtc { get; init; } public DateTime? PunchOutAtUtc { get; init; }
    public AttendanceSessionStatus Status { get; init; }
    public string? PunchInNote { get; init; } public string? PunchOutNote { get; init; }
    public string? PunchInPhotoKey { get; init; } public string? PunchOutPhotoKey { get; init; }
    public string? AdjustmentReason { get; init; }
    public DateTime? OriginalPunchInAtUtc { get; init; } public DateTime? OriginalPunchOutAtUtc { get; init; }
}
