using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Application.Features.Meetings;

public sealed record GetOrganizationMeetingsQuery(int OrganizationId, DateTimeOffset FromUtc,
    DateTimeOffset ToUtc, MeetingStatus? Status = null, string? Search = null, int Skip = 0, int Take = 20)
    : IRequest<MeetingPageDto>, IOrganizationScopedRequest;
public sealed record GetMeetingDetailQuery(int MeetingId) : IRequest<MeetingDetailDto>;
public sealed record GetMeetingAccessLinksQuery(int MeetingId) : IRequest<IReadOnlyList<MeetingAccessLinkDto>>;

public sealed class GetOrganizationMeetingsQueryValidator : AbstractValidator<GetOrganizationMeetingsQuery>
{
    public GetOrganizationMeetingsQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x).Must(x => x.ToUtc > x.FromUtc).WithMessage("To must be after from.");
        RuleFor(x => x).Must(x => x.ToUtc - x.FromUtc <= TimeSpan.FromDays(366))
            .WithMessage("Meeting list windows cannot exceed 366 days.");
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Search).MaximumLength(160); RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 100);
    }
}

public sealed class GetOrganizationMeetingsQueryHandler(ISqlConnectionFactory sql,
    ICurrentUserService user, IOrganizationPermissionChecker permissions)
    : IRequestHandler<GetOrganizationMeetingsQuery, MeetingPageDto>
{
    public async Task<MeetingPageDto> Handle(GetOrganizationMeetingsQuery request, CancellationToken ct)
    {
        var canManage = await permissions.HasPermissionAsync(request.OrganizationId, user.UserId,
            OrganizationPermissionNames.ManageMeetings, ct);
        const string query = """
            WITH visible AS (
                SELECT m."Id", m."OrganizationId", m."Title", m."Description", m."Status",
                       m."ScheduledStartUtc", m."ScheduledEndUtc", m."TimeZone", m."ActualStartUtc",
                       m."ActualEndUtc", m."CreatedByUserId", u."FirstName" || ' ' || u."LastName" AS "CreatorName",
                       (SELECT COUNT(*)::int FROM "MeetingParticipants" p
                        WHERE p."MeetingId" = m."Id" AND p."IsDeleted" = FALSE) AS "ParticipantCount"
                FROM "Meetings" m
                JOIN "Users" u ON u."Id" = m."CreatedByUserId"
                WHERE m."OrganizationId" = @OrganizationId AND m."IsDeleted" = FALSE
                  AND (@Status IS NULL OR m."Status" = @Status)
                  AND (@Search IS NULL OR m."Title" ILIKE '%' || @Search || '%'
                       OR COALESCE(m."Description", '') ILIKE '%' || @Search || '%')
                  AND COALESCE(m."ScheduledStartUtc", m."ActualStartUtc", m."CreatedAt") < @ToUtc
                  AND COALESCE(m."ScheduledEndUtc", m."ActualEndUtc", m."ScheduledStartUtc",
                               m."ActualStartUtc", m."CreatedAt") >= @FromUtc
                  AND (@CanManage OR m."CreatedByUserId" = @UserId OR EXISTS (
                       SELECT 1 FROM "MeetingParticipants" mp WHERE mp."MeetingId" = m."Id"
                         AND mp."UserId" = @UserId AND mp."State" <> 3 AND mp."IsDeleted" = FALSE))
            )
            SELECT visible.* FROM visible
            ORDER BY COALESCE("ScheduledStartUtc", "ActualStartUtc") NULLS FIRST, "Id" DESC
            OFFSET @Skip LIMIT @Take;
            """;
        const string countQuery = """
            SELECT COUNT(*)::int
            FROM "Meetings" m
            WHERE m."OrganizationId" = @OrganizationId AND m."IsDeleted" = FALSE
              AND (@Status IS NULL OR m."Status" = @Status)
              AND (@Search IS NULL OR m."Title" ILIKE '%' || @Search || '%'
                   OR COALESCE(m."Description", '') ILIKE '%' || @Search || '%')
              AND COALESCE(m."ScheduledStartUtc", m."ActualStartUtc", m."CreatedAt") < @ToUtc
              AND COALESCE(m."ScheduledEndUtc", m."ActualEndUtc", m."ScheduledStartUtc",
                           m."ActualStartUtc", m."CreatedAt") >= @FromUtc
              AND (@CanManage OR m."CreatedByUserId" = @UserId OR EXISTS (
                   SELECT 1 FROM "MeetingParticipants" mp WHERE mp."MeetingId" = m."Id"
                     AND mp."UserId" = @UserId AND mp."State" <> 3 AND mp."IsDeleted" = FALSE));
            """;
        using var connection = sql.Create();
        var parameters = new
        {
            request.OrganizationId, UserId = user.UserId, CanManage = canManage,
            Status = request.Status.HasValue ? (int?)request.Status.Value : null,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            FromUtc = request.FromUtc.UtcDateTime, ToUtc = request.ToUtc.UtcDateTime,
            request.Skip, request.Take
        };
        var total = await connection.QuerySingleAsync<int>(new CommandDefinition(countQuery, parameters,
            cancellationToken: ct));
        var rows = (await connection.QueryAsync<MeetingListRow>(new CommandDefinition(query, parameters,
            cancellationToken: ct))).ToList();
        var items = rows.Select(x => new MeetingListItemDto(x.Id, x.OrganizationId, x.Title,
            x.Description, x.Status, x.ScheduledStartUtc, x.ScheduledEndUtc, x.TimeZone,
            x.ActualStartUtc, x.ActualEndUtc, x.CreatedByUserId, x.CreatorName, x.ParticipantCount)).ToList();
        return new(items, total, request.Skip, request.Take);
    }

    private sealed class MeetingListRow
    {
        public int Id { get; init; } public int OrganizationId { get; init; }
        public string Title { get; init; } = string.Empty; public string? Description { get; init; }
        public MeetingStatus Status { get; init; } public DateTime? ScheduledStartUtc { get; init; }
        public DateTime? ScheduledEndUtc { get; init; } public string TimeZone { get; init; } = "UTC";
        public DateTime? ActualStartUtc { get; init; } public DateTime? ActualEndUtc { get; init; }
        public int CreatedByUserId { get; init; } public string CreatorName { get; init; } = string.Empty;
        public int ParticipantCount { get; init; }
    }
}

public sealed class GetMeetingDetailQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
    IOrganizationPermissionChecker permissions) : IRequestHandler<GetMeetingDetailQuery, MeetingDetailDto>
{
    public async Task<MeetingDetailDto> Handle(GetMeetingDetailQuery request, CancellationToken ct)
    {
        const string query = """
            SELECT m."Id", m."OrganizationId", m."CreatedByUserId",
                   u."FirstName" || ' ' || u."LastName" AS "CreatorName", m."Title", m."Description",
                   m."ScheduledStartUtc", m."ScheduledEndUtc", m."TimeZone", m."Status",
                   m."ActualStartUtc", m."ActualEndUtc", m."LobbyEnabled", m."GuestsAllowed",
                   m."ParticipantsCanPublish", m."ParticipantsCanShareScreen",
                   m."ParticipantsCanEditNote", m."ViewersCanChat", m."RetentionDays",
                   EXISTS (SELECT 1 FROM "MeetingParticipants" access
                           WHERE access."MeetingId" = m."Id" AND access."UserId" = @UserId
                             AND access."State" <> 3 AND access."IsDeleted" = FALSE) AS "IsParticipant"
            FROM "Meetings" m JOIN "Users" u ON u."Id" = m."CreatedByUserId"
            WHERE m."Id" = @MeetingId AND m."IsDeleted" = FALSE;
            SELECT b."Id", b."Label", b."Color", b."Icon" FROM "MeetingBadgeDefinitions" b
            WHERE b."MeetingId" = @MeetingId AND b."IsDeleted" = FALSE ORDER BY b."Id";
            SELECT p."Id", p."UserId", COALESCE(p."DisplayName", u."FirstName" || ' ' || u."LastName") AS "DisplayName",
                   COALESCE(p."NormalizedEmail", u."Email") AS "Email", p."AccessLevel",
                   p."BadgeDefinitionId", p."State"
            FROM "MeetingParticipants" p LEFT JOIN "Users" u ON u."Id" = p."UserId"
            WHERE p."MeetingId" = @MeetingId AND p."IsDeleted" = FALSE ORDER BY p."AccessLevel", p."Id";
            """;
        using var connection = sql.Create();
        using var results = await connection.QueryMultipleAsync(new CommandDefinition(query,
            new { request.MeetingId, UserId = user.UserId }, cancellationToken: ct));
        var row = await results.ReadSingleOrDefaultAsync<MeetingDetailRow>() ??
            throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var canManage = row.CreatedByUserId == user.UserId || await permissions.HasPermissionAsync(
            row.OrganizationId, user.UserId, OrganizationPermissionNames.ManageMeetings, ct);
        if (!canManage && !row.IsParticipant)
            throw new ForbiddenException("MEETING_ACCESS_DENIED", "You do not have access to this meeting.");
        var badges = (await results.ReadAsync<MeetingBadgeDto>()).ToList();
        var participants = (await results.ReadAsync<MeetingParticipantDto>()).ToList();
        return new(row.Id, row.OrganizationId, row.CreatedByUserId, row.CreatorName, row.Title,
            row.Description, row.ScheduledStartUtc, row.ScheduledEndUtc, row.TimeZone, row.Status,
            row.ActualStartUtc, row.ActualEndUtc, row.LobbyEnabled, row.GuestsAllowed,
            row.ParticipantsCanPublish, row.ParticipantsCanShareScreen, row.ParticipantsCanEditNote,
            row.ViewersCanChat, row.RetentionDays, canManage, badges, participants);
    }
    private sealed class MeetingDetailRow
    {
        public int Id { get; init; } public int OrganizationId { get; init; } public int CreatedByUserId { get; init; }
        public string CreatorName { get; init; } = string.Empty; public string Title { get; init; } = string.Empty;
        public string? Description { get; init; } public DateTime? ScheduledStartUtc { get; init; }
        public DateTime? ScheduledEndUtc { get; init; } public string TimeZone { get; init; } = "UTC";
        public MeetingStatus Status { get; init; } public DateTime? ActualStartUtc { get; init; }
        public DateTime? ActualEndUtc { get; init; } public bool LobbyEnabled { get; init; }
        public bool GuestsAllowed { get; init; } public bool ParticipantsCanPublish { get; init; }
        public bool ParticipantsCanShareScreen { get; init; } public bool ParticipantsCanEditNote { get; init; }
        public bool ViewersCanChat { get; init; } public int RetentionDays { get; init; } public bool IsParticipant { get; init; }
    }
}

public sealed class GetMeetingAccessLinksQueryHandler(ISqlConnectionFactory sql, ICurrentUserService user,
    IOrganizationPermissionChecker permissions) : IRequestHandler<GetMeetingAccessLinksQuery, IReadOnlyList<MeetingAccessLinkDto>>
{
    public async Task<IReadOnlyList<MeetingAccessLinkDto>> Handle(GetMeetingAccessLinksQuery request, CancellationToken ct)
    {
        const string meetingSql = "SELECT \"OrganizationId\", \"CreatedByUserId\" FROM \"Meetings\" WHERE \"Id\"=@MeetingId AND \"IsDeleted\"=FALSE;";
        using var connection = sql.Create();
        var scope = await connection.QuerySingleOrDefaultAsync<MeetingScope>(new CommandDefinition(meetingSql,
            new { request.MeetingId }, cancellationToken: ct)) ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        if (scope.CreatedByUserId != user.UserId)
            await permissions.EnsurePermissionAsync(scope.OrganizationId, user.UserId,
                OrganizationPermissionNames.ManageMeetings, ct);
        const string linksSql = """
            SELECT "Id", "Mode", "LockedEmail", "DefaultAccessLevel", "BadgeDefinitionId",
                   "ExpiresAtUtc", "MaximumUses", "UseCount", "RevokedAtUtc"
            FROM "MeetingAccessLinks" WHERE "MeetingId"=@MeetingId AND "IsDeleted"=FALSE ORDER BY "CreatedAt" DESC;
            """;
        return (await connection.QueryAsync<MeetingAccessLinkDto>(new CommandDefinition(linksSql,
            new { request.MeetingId }, cancellationToken: ct))).ToList();
    }
    private sealed record MeetingScope(int OrganizationId, int CreatedByUserId);
}
