using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Tracking;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Attendance;

/// <summary>Whether the current member must be tracked, and what the app should do about it.</summary>
public sealed record GetMyFieldTrackingStatusQuery(int OrganizationId)
    : IRequest<MyFieldTrackingStatusDto>, IOrganizationScopedRequest;

/// <summary>Tracking settings and the role list: ManageAttendance or ViewFieldTracking, checked in the handler.</summary>
public sealed record GetFieldTrackingSettingsQuery(int OrganizationId)
    : IRequest<FieldTrackingSettingsDto>, IOrganizationScopedRequest;

/// <summary>Where every tracked member who is punched in was last seen: ViewFieldTracking.</summary>
public sealed record GetLiveFieldTrackingQuery(int OrganizationId)
    : IRequest<LiveFieldTrackingDto>, IOrganizationScopedRequest;

/// <summary>One member's route for one work date: the member themself, or ViewFieldTracking.</summary>
public sealed record GetFieldTrackingDayQuery(int OrganizationId, int UserId, DateOnly? Date)
    : IRequest<FieldTrackingDayDto>, IOrganizationScopedRequest;

/// <summary>The current member's own route for one work date.</summary>
public sealed record GetMyFieldTrackingDayQuery(int OrganizationId, DateOnly? Date)
    : IRequest<FieldTrackingDayDto>, IOrganizationScopedRequest;

public sealed class GetFieldTrackingDayQueryValidator : AbstractValidator<GetFieldTrackingDayQuery>
{
    public GetFieldTrackingDayQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.UserId).GreaterThan(0);
    }
}

public sealed class GetMyFieldTrackingStatusQueryHandler : IRequestHandler<GetMyFieldTrackingStatusQuery, MyFieldTrackingStatusDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    public GetMyFieldTrackingStatusQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user) => (_sql, _user) = (sql, user);
    public async Task<MyFieldTrackingStatusDto> Handle(GetMyFieldTrackingStatusQuery request, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
              COALESCE((SELECT s."TrackingEnabled" FROM "AttendanceSettings" s
                        WHERE s."OrganizationId" = @OrganizationId AND s."IsDeleted" = FALSE), FALSE) AS "TrackingEnabled",
              EXISTS (SELECT 1 FROM "OrganizationMembers" m
                      JOIN "AttendanceTrackedRoles" t ON t."OrganizationId" = m."OrganizationId"
                       AND t."OrganizationRoleId" = m."OrganizationRoleId" AND t."IsDeleted" = FALSE
                      WHERE m."OrganizationId" = @OrganizationId AND m."UserId" = @UserId
                        AND m."IsActive" = TRUE AND m."IsDeleted" = FALSE) AS "IsTrackedRole",
              EXISTS (SELECT 1 FROM "FieldTrackingConsents" c
                      WHERE c."OrganizationId" = @OrganizationId AND c."UserId" = @UserId
                        AND c."PolicyVersion" = @PolicyVersion AND c."IsDeleted" = FALSE) AS "HasConsented",
              (SELECT a."Id" FROM "AttendanceSessions" a
               WHERE a."OrganizationId" = @OrganizationId AND a."UserId" = @UserId
                 AND a."Status" = @Open AND a."IsDeleted" = FALSE LIMIT 1) AS "OpenSessionId",
              COALESCE((SELECT a."IsTracked" FROM "AttendanceSessions" a
                        WHERE a."OrganizationId" = @OrganizationId AND a."UserId" = @UserId
                          AND a."Status" = @Open AND a."IsDeleted" = FALSE LIMIT 1), FALSE) AS "OpenSessionIsTracked";
            """;
        using var connection = _sql.Create();
        var row = await connection.QuerySingleAsync<StatusRow>(new CommandDefinition(sql, new
        {
            request.OrganizationId,
            UserId = _user.UserId,
            PolicyVersion = FieldTrackingPolicy.CurrentVersion,
            Open = (int)AttendanceSessionStatus.Open
        }, cancellationToken: cancellationToken));
        return new MyFieldTrackingStatusDto(row.TrackingEnabled, row.IsTrackedRole, row.TrackingEnabled && row.IsTrackedRole,
            FieldTrackingPolicy.CurrentVersion, row.HasConsented, row.OpenSessionId, row.OpenSessionIsTracked,
            FieldTrackingPolicy.MovingIntervalSeconds, FieldTrackingPolicy.MovingDistanceMeters,
            FieldTrackingPolicy.StillIntervalSeconds, FieldTrackingPolicy.UploadIntervalSeconds,
            FieldTrackingPolicy.MaxPointsPerUpload);
    }

    public sealed class StatusRow
    {
        public bool TrackingEnabled { get; init; } public bool IsTrackedRole { get; init; }
        public bool HasConsented { get; init; } public int? OpenSessionId { get; init; }
        public bool OpenSessionIsTracked { get; init; }
    }
}

public sealed class GetFieldTrackingSettingsQueryHandler : IRequestHandler<GetFieldTrackingSettingsQuery, FieldTrackingSettingsDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions; private readonly IRouteMatcher _matcher;
    public GetFieldTrackingSettingsQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions, IRouteMatcher matcher) =>
        (_sql, _user, _permissions, _matcher) = (sql, user, permissions, matcher);
    public async Task<FieldTrackingSettingsDto> Handle(GetFieldTrackingSettingsQuery request, CancellationToken cancellationToken)
    {
        await FieldTrackingReads.EnsureManageOrViewAsync(_permissions, request.OrganizationId, _user.UserId, cancellationToken);
        const string settingsSql = """
            SELECT s."TrackingEnabled", s."LocationRetentionDays"
            FROM "AttendanceSettings" s
            WHERE s."OrganizationId" = @OrganizationId AND s."IsDeleted" = FALSE;
            """;
        const string rolesSql = """
            SELECT r."Id" AS "OrganizationRoleId", r."Name",
                   EXISTS (SELECT 1 FROM "AttendanceTrackedRoles" t
                           WHERE t."OrganizationId" = r."OrganizationId" AND t."OrganizationRoleId" = r."Id"
                             AND t."IsDeleted" = FALSE) AS "IsTracked",
                   (SELECT COUNT(*) FROM "OrganizationMembers" m
                    WHERE m."OrganizationRoleId" = r."Id" AND m."IsActive" = TRUE AND m."IsDeleted" = FALSE)::int AS "MemberCount"
            FROM "OrganizationRoles" r
            WHERE r."OrganizationId" = @OrganizationId AND r."IsDeleted" = FALSE
            ORDER BY r."Name";
            """;
        using var connection = _sql.Create();
        var settings = await connection.QuerySingleOrDefaultAsync<SettingsRow>(new CommandDefinition(settingsSql,
            new { request.OrganizationId }, cancellationToken: cancellationToken));
        var roles = await connection.QueryAsync<RoleRow>(new CommandDefinition(rolesSql,
            new { request.OrganizationId }, cancellationToken: cancellationToken));
        return new FieldTrackingSettingsDto(request.OrganizationId, settings?.TrackingEnabled ?? false,
            settings?.LocationRetentionDays ?? AttendanceSettings.DefaultLocationRetentionDays,
            FieldTrackingPolicy.CurrentVersion, _matcher.IsConfigured,
            roles.Select(x => new FieldTrackingRoleDto(x.OrganizationRoleId, x.Name, x.IsTracked, x.MemberCount)).ToList());
    }

    public sealed class SettingsRow { public bool TrackingEnabled { get; init; } public int LocationRetentionDays { get; init; } }

    public sealed class RoleRow
    {
        public int OrganizationRoleId { get; init; } public string Name { get; init; } = string.Empty;
        public bool IsTracked { get; init; } public int MemberCount { get; init; }
    }
}

public sealed class GetLiveFieldTrackingQueryHandler : IRequestHandler<GetLiveFieldTrackingQuery, LiveFieldTrackingDto>
{
    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions;
    public GetLiveFieldTrackingQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions) => (_sql, _user, _permissions) = (sql, user, permissions);
    public async Task<LiveFieldTrackingDto> Handle(GetLiveFieldTrackingQuery request, CancellationToken cancellationToken)
    {
        await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
            OrganizationPermissionNames.ViewFieldTracking, cancellationToken);
        // One row per tracked open session, with its newest fix (the unique session+time index serves the lateral).
        const string sql = """
            SELECT s."UserId", u."FirstName" || ' ' || u."LastName" AS "FullName",
                   COALESCE(r."Name", 'Owner') AS "RoleName", s."Id" AS "AttendanceSessionId", s."PunchInAtUtc",
                   s."PunchInLatitude", s."PunchInLongitude", s."PunchInAccuracyMeters",
                   p."Latitude", p."Longitude", p."AccuracyMeters", p."RecordedAtUtc", p."BatteryPercent",
                   p."IsCharging", COALESCE(p."IsMock", FALSE) AS "IsMock"
            FROM "AttendanceSessions" s
            JOIN "Users" u ON u."Id" = s."UserId"
            LEFT JOIN "OrganizationMembers" m ON m."OrganizationId" = s."OrganizationId" AND m."UserId" = s."UserId"
                 AND m."IsDeleted" = FALSE
            LEFT JOIN "OrganizationRoles" r ON r."Id" = m."OrganizationRoleId"
            LEFT JOIN LATERAL (
                SELECT lp."Latitude", lp."Longitude", lp."AccuracyMeters", lp."RecordedAtUtc", lp."BatteryPercent",
                       lp."IsCharging", lp."IsMock"
                FROM "LocationPoints" lp
                WHERE lp."AttendanceSessionId" = s."Id"
                ORDER BY lp."RecordedAtUtc" DESC
                LIMIT 1) p ON TRUE
            WHERE s."OrganizationId" = @OrganizationId AND s."Status" = @Open AND s."IsTracked" = TRUE
              AND s."IsDeleted" = FALSE
            ORDER BY u."FirstName", u."LastName";
            """;
        using var connection = _sql.Create();
        var rows = await connection.QueryAsync<LiveRow>(new CommandDefinition(sql, new
        {
            request.OrganizationId,
            Open = (int)AttendanceSessionStatus.Open
        }, cancellationToken: cancellationToken));
        var members = rows.Select(x =>
        {
            var hasFix = x.Latitude is not null && x.Longitude is not null;
            return new LiveFieldMemberDto(x.UserId, x.FullName, x.RoleName, x.AttendanceSessionId,
                AttendanceReads.Utc(x.PunchInAtUtc),
                hasFix ? x.Latitude : x.PunchInLatitude, hasFix ? x.Longitude : x.PunchInLongitude,
                hasFix ? x.AccuracyMeters : x.PunchInAccuracyMeters,
                x.RecordedAtUtc is { } seen ? AttendanceReads.Utc(seen) : null,
                x.BatteryPercent, x.IsCharging, x.IsMock, !hasFix && x.PunchInLatitude is not null);
        }).ToList();
        return new LiveFieldTrackingDto(DateTime.UtcNow, members);
    }

    public sealed class LiveRow
    {
        public int UserId { get; init; } public string FullName { get; init; } = string.Empty;
        public string RoleName { get; init; } = string.Empty; public int AttendanceSessionId { get; init; }
        public DateTime PunchInAtUtc { get; init; }
        public double? PunchInLatitude { get; init; } public double? PunchInLongitude { get; init; }
        public double? PunchInAccuracyMeters { get; init; }
        public double? Latitude { get; init; } public double? Longitude { get; init; } public double? AccuracyMeters { get; init; }
        public DateTime? RecordedAtUtc { get; init; } public int? BatteryPercent { get; init; }
        public bool? IsCharging { get; init; } public bool IsMock { get; init; }
    }
}

public sealed class GetMyFieldTrackingDayQueryHandler : IRequestHandler<GetMyFieldTrackingDayQuery, FieldTrackingDayDto>
{
    private readonly ISender _sender; private readonly ICurrentUserService _user;
    public GetMyFieldTrackingDayQueryHandler(ISender sender, ICurrentUserService user) => (_sender, _user) = (sender, user);
    public Task<FieldTrackingDayDto> Handle(GetMyFieldTrackingDayQuery request, CancellationToken cancellationToken) =>
        _sender.Send(new GetFieldTrackingDayQuery(request.OrganizationId, _user.UserId, request.Date), cancellationToken);
}

public sealed class GetFieldTrackingDayQueryHandler : IRequestHandler<GetFieldTrackingDayQuery, FieldTrackingDayDto>
{
    private const int MaxPendingPoints = 3000;
    private static readonly TimeSpan BatteryBucket = TimeSpan.FromMinutes(5);

    private readonly ISqlConnectionFactory _sql; private readonly ICurrentUserService _user;
    private readonly IOrganizationPermissionChecker _permissions;
    public GetFieldTrackingDayQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
        IOrganizationPermissionChecker permissions) => (_sql, _user, _permissions) = (sql, user, permissions);
    public async Task<FieldTrackingDayDto> Handle(GetFieldTrackingDayQuery request, CancellationToken cancellationToken)
    {
        // A member may always see their own route (transparency); anyone else needs ViewFieldTracking.
        if (request.UserId != _user.UserId)
            await _permissions.EnsurePermissionAsync(request.OrganizationId, _user.UserId,
                OrganizationPermissionNames.ViewFieldTracking, cancellationToken);

        using var connection = _sql.Create();
        var settings = await AttendanceReads.LoadSettingsAsync(connection, request.OrganizationId, cancellationToken);
        var date = request.Date ?? AttendanceClock.WorkDate(DateTime.UtcNow, AttendanceClock.Resolve(settings.TimeZone));

        const string userSql = """
            SELECT u."FirstName" || ' ' || u."LastName"
            FROM "Users" u
            WHERE u."Id" = @UserId AND u."IsDeleted" = FALSE;
            """;
        var fullName = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(userSql,
            new { request.UserId }, cancellationToken: cancellationToken)) ??
            throw new NotFoundException("USER_NOT_FOUND", "Member not found.");

        const string sessionsSql = """
            SELECT s."Id", s."PunchInAtUtc", s."PunchOutAtUtc", s."Status", s."IsTracked",
                   s."PunchInLatitude", s."PunchInLongitude", s."PunchOutLatitude", s."PunchOutLongitude"
            FROM "AttendanceSessions" s
            WHERE s."OrganizationId" = @OrganizationId AND s."UserId" = @UserId AND s."WorkDate" = @Date::date
              AND s."IsDeleted" = FALSE
            ORDER BY s."PunchInAtUtc", s."Id";
            """;
        var sessions = (await connection.QueryAsync<SessionRow>(new CommandDefinition(sessionsSql, new
        {
            request.OrganizationId,
            request.UserId,
            Date = date.ToDateTime(TimeOnly.MinValue)
        }, cancellationToken: cancellationToken))).ToList();
        var ids = sessions.Select(x => x.Id).ToArray();

        const string segmentsSql = """
            SELECT g."AttendanceSessionId", g."Sequence", g."Kind", g."StartedAtUtc", g."EndedAtUtc",
                   g."EncodedPolyline", g."DistanceMeters", g."IsSnapped"
            FROM "TrackedRouteSegments" g
            WHERE g."AttendanceSessionId" = ANY(@Ids)
            ORDER BY g."AttendanceSessionId", g."Sequence";
            """;
        const string stopsSql = """
            SELECT t."AttendanceSessionId", t."Latitude", t."Longitude", t."ArrivedAtUtc", t."LeftAtUtc"
            FROM "TrackStops" t
            WHERE t."AttendanceSessionId" = ANY(@Ids)
            ORDER BY t."ArrivedAtUtc";
            """;
        const string pointsSql = """
            SELECT p."AttendanceSessionId", p."RecordedAtUtc", p."Latitude", p."Longitude", p."AccuracyMeters",
                   p."IsMock", p."BatteryPercent", p."IsCharging", p."IsProcessed"
            FROM "LocationPoints" p
            WHERE p."AttendanceSessionId" = ANY(@Ids)
            ORDER BY p."RecordedAtUtc";
            """;
        var segments = ids.Length == 0 ? new List<SegmentRow>() : (await connection.QueryAsync<SegmentRow>(new CommandDefinition(segmentsSql,
            new { Ids = ids }, cancellationToken: cancellationToken))).ToList();
        var stops = ids.Length == 0 ? new List<StopRow>() : (await connection.QueryAsync<StopRow>(new CommandDefinition(stopsSql,
            new { Ids = ids }, cancellationToken: cancellationToken))).ToList();
        var points = ids.Length == 0 ? new List<PointRow>() : (await connection.QueryAsync<PointRow>(new CommandDefinition(pointsSql,
            new { Ids = ids }, cancellationToken: cancellationToken))).ToList();

        var sessionDtos = sessions.Select(session => new TrackedSessionDto(session.Id,
            AttendanceReads.Utc(session.PunchInAtUtc),
            session.PunchOutAtUtc is { } end ? AttendanceReads.Utc(end) : null,
            session.Status, session.IsTracked, session.PunchInLatitude, session.PunchInLongitude,
            session.PunchOutLatitude, session.PunchOutLongitude,
            segments.Where(x => x.AttendanceSessionId == session.Id)
                .Select(x => new TrackedSegmentDto(x.Sequence, x.Kind, AttendanceReads.Utc(x.StartedAtUtc),
                    AttendanceReads.Utc(x.EndedAtUtc), x.EncodedPolyline, x.DistanceMeters, x.IsSnapped)).ToList(),
            stops.Where(x => x.AttendanceSessionId == session.Id)
                .Select(x => new TrackStopDto(x.Latitude, x.Longitude, AttendanceReads.Utc(x.ArrivedAtUtc),
                    AttendanceReads.Utc(x.LeftAtUtc), Math.Round((x.LeftAtUtc - x.ArrivedAtUtc).TotalMinutes, 1))).ToList(),
            points.Where(x => x.AttendanceSessionId == session.Id && !x.IsProcessed).TakeLast(MaxPendingPoints)
                .Select(x => new TrackPointDto(AttendanceReads.Utc(x.RecordedAtUtc), x.Latitude, x.Longitude,
                    x.AccuracyMeters, x.IsMock)).ToList()))
            .ToList();

        return new FieldTrackingDayDto(request.UserId, fullName, date, settings.TimeZone,
            // Travelled is everything the member's own points prove, whether or not a road
            // explains it: a train ride is travel. Only a gap - which nobody has any record of -
            // is counted as guessed.
            segments.Where(x => x.Kind != TrackedSegmentKind.Inferred).Sum(x => x.DistanceMeters),
            segments.Where(x => x.Kind == TrackedSegmentKind.Inferred).Sum(x => x.DistanceMeters),
            stops.Count, points.Count(x => x.IsMock), points.Count, SampleBattery(points), sessionDtos);
    }

    /// <summary>The first reading in each five-minute bucket, plus the latest, so a day draws as a line, not 6 000 dots.</summary>
    private static List<BatterySampleDto> SampleBattery(IReadOnlyList<PointRow> points)
    {
        var samples = new List<BatterySampleDto>();
        DateTime? bucketStart = null;
        PointRow? last = null;
        foreach (var point in points.Where(x => x.BatteryPercent is not null))
        {
            last = point;
            if (bucketStart is { } start && point.RecordedAtUtc - start < BatteryBucket) continue;
            bucketStart = point.RecordedAtUtc;
            samples.Add(new BatterySampleDto(AttendanceReads.Utc(point.RecordedAtUtc), point.BatteryPercent!.Value, point.IsCharging));
        }
        if (last is not null && (samples.Count == 0 || samples[^1].RecordedAtUtc != AttendanceReads.Utc(last.RecordedAtUtc)))
            samples.Add(new BatterySampleDto(AttendanceReads.Utc(last.RecordedAtUtc), last.BatteryPercent!.Value, last.IsCharging));
        return samples;
    }

    public sealed class SessionRow
    {
        public int Id { get; init; } public DateTime PunchInAtUtc { get; init; } public DateTime? PunchOutAtUtc { get; init; }
        public AttendanceSessionStatus Status { get; init; } public bool IsTracked { get; init; }
        public double? PunchInLatitude { get; init; } public double? PunchInLongitude { get; init; }
        public double? PunchOutLatitude { get; init; } public double? PunchOutLongitude { get; init; }
    }

    public sealed class SegmentRow
    {
        public int AttendanceSessionId { get; init; } public int Sequence { get; init; } public TrackedSegmentKind Kind { get; init; }
        public DateTime StartedAtUtc { get; init; } public DateTime EndedAtUtc { get; init; }
        public string EncodedPolyline { get; init; } = string.Empty; public double DistanceMeters { get; init; }
        public bool IsSnapped { get; init; }
    }

    public sealed class StopRow
    {
        public int AttendanceSessionId { get; init; } public double Latitude { get; init; } public double Longitude { get; init; }
        public DateTime ArrivedAtUtc { get; init; } public DateTime LeftAtUtc { get; init; }
    }

    public sealed class PointRow
    {
        public int AttendanceSessionId { get; init; } public DateTime RecordedAtUtc { get; init; }
        public double Latitude { get; init; } public double Longitude { get; init; } public double? AccuracyMeters { get; init; }
        public bool IsMock { get; init; } public int? BatteryPercent { get; init; } public bool? IsCharging { get; init; }
        public bool IsProcessed { get; init; }
    }
}

internal static class FieldTrackingReads
{
    public static async Task EnsureManageOrViewAsync(IOrganizationPermissionChecker permissions, int organizationId, int userId,
        CancellationToken cancellationToken)
    {
        if (await permissions.HasPermissionAsync(organizationId, userId, OrganizationPermissionNames.ManageAttendance, cancellationToken) ||
            await permissions.HasPermissionAsync(organizationId, userId, OrganizationPermissionNames.ViewFieldTracking, cancellationToken))
            return;
        throw new ForbiddenException("PERMISSION_DENIED",
            "Seeing field tracking settings needs the ManageAttendance or ViewFieldTracking permission.");
    }
}
