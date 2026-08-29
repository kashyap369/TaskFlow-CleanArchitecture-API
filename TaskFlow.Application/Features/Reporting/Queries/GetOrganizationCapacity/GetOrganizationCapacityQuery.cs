using Dapper;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetOrganizationCapacity;

/// <summary>
/// Monday-based weekly capacity. Each open assigned task contributes its full
/// estimate to the UTC calendar week containing its due date, or its start date
/// when it has no due date. This intentionally avoids speculative spreading or
/// forecasting; future phases may introduce a richer allocation model.
/// </summary>
public sealed record GetOrganizationCapacityQuery(
    int OrganizationId,
    DateOnly WeekStart,
    int Weeks = 6
) : IRequest<IReadOnlyList<CapacityDto>>, IOrganizationScopedRequest;

public sealed class GetOrganizationCapacityQueryValidator
    : AbstractValidator<GetOrganizationCapacityQuery>
{
    public GetOrganizationCapacityQueryValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        RuleFor(x => x.WeekStart)
            .Must(value => value.DayOfWeek == DayOfWeek.Monday)
            .WithMessage("Week start must be a Monday.");
        RuleFor(x => x.Weeks).InclusiveBetween(1, 12);
    }
}

public sealed class GetOrganizationCapacityQueryHandler
    : IRequestHandler<GetOrganizationCapacityQuery, IReadOnlyList<CapacityDto>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    public GetOrganizationCapacityQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
    }

    public async Task<IReadOnlyList<CapacityDto>> Handle(
        GetOrganizationCapacityQuery request,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH weeks AS (
                SELECT generate_series(
                    @WeekStart::date,
                    @WeekStart::date + ((@Weeks - 1) * INTERVAL '7 days'),
                    INTERVAL '7 days'
                )::date AS "WeekStart"
            ), member_weeks AS (
                SELECT
                    m."OrganizationId",
                    m."UserId",
                    u."FirstName" || ' ' || u."LastName" AS "MemberName",
                    m."WeeklyCapacityMinutes",
                    w."WeekStart"
                FROM "OrganizationMembers" m
                JOIN "Users" u ON u."Id" = m."UserId"
                CROSS JOIN weeks w
                WHERE m."OrganizationId" = @OrganizationId
                  AND m."IsActive" = TRUE
                  AND m."IsDeleted" = FALSE
            ), allocation AS (
                SELECT
                    mw."OrganizationId",
                    mw."UserId",
                    mw."MemberName",
                    mw."WeeklyCapacityMinutes",
                    mw."WeekStart",
                    COUNT(t."Id")::int AS "AssignedTaskCount",
                    COUNT(t."Id") FILTER (WHERE t."EstimateMinutes" IS NULL)::int
                        AS "MissingEstimateTaskCount",
                    COALESCE(SUM(t."EstimateMinutes"), 0)::int AS "KnownEstimateMinutes"
                FROM member_weeks mw
                LEFT JOIN "Tasks" t
                  ON t."OrganizationId" = mw."OrganizationId"
                 AND t."AssignedToUserId" = mw."UserId"
                 AND t."IsDeleted" = FALSE
                 AND t."Status" NOT IN (3, 5)
                 AND (COALESCE(t."ExpectedCompletionDate", t."StartDate") AT TIME ZONE 'UTC')::date
                     >= mw."WeekStart"
                 AND (COALESCE(t."ExpectedCompletionDate", t."StartDate") AT TIME ZONE 'UTC')::date
                     < mw."WeekStart" + 7
                GROUP BY mw."OrganizationId", mw."UserId", mw."MemberName",
                         mw."WeeklyCapacityMinutes", mw."WeekStart"
            )
            SELECT
                "OrganizationId" AS "OrganizationId",
                "UserId" AS "UserId",
                "MemberName" AS "MemberName",
                "WeekStart" AS "WeekStart",
                ("WeekStart" + 6) AS "WeekEnd",
                "WeeklyCapacityMinutes" AS "WeeklyCapacityMinutes",
                CASE WHEN "WeeklyCapacityMinutes" IS NULL OR "MissingEstimateTaskCount" > 0
                     THEN NULL ELSE "KnownEstimateMinutes" END AS "AssignedEstimateMinutes",
                CASE WHEN "WeeklyCapacityMinutes" IS NULL OR "MissingEstimateTaskCount" > 0
                     THEN NULL ELSE "WeeklyCapacityMinutes" - "KnownEstimateMinutes" END
                     AS "RemainingCapacityMinutes",
                "AssignedTaskCount" AS "AssignedTaskCount",
                "MissingEstimateTaskCount" AS "MissingEstimateTaskCount",
                ("WeeklyCapacityMinutes" IS NOT NULL AND "MissingEstimateTaskCount" = 0)
                    AS "HasEnoughData",
                CASE
                    WHEN "WeeklyCapacityMinutes" IS NULL OR "MissingEstimateTaskCount" > 0
                        THEN 'NotEnoughData'
                    WHEN "WeeklyCapacityMinutes" = 0 AND "KnownEstimateMinutes" > 0 THEN 'Heavy'
                    WHEN "WeeklyCapacityMinutes" = 0 THEN 'Light'
                    WHEN "KnownEstimateMinutes" * 100 <= "WeeklyCapacityMinutes" * 70 THEN 'Light'
                    WHEN "KnownEstimateMinutes" <= "WeeklyCapacityMinutes" THEN 'Balanced'
                    ELSE 'Heavy'
                END AS "WorkloadState"
            FROM allocation
            ORDER BY "MemberName", "UserId", "WeekStart";
            """;

        using var connection = _sqlConnectionFactory.Create();
        var rows = await connection.QueryAsync<CapacityDto>(new CommandDefinition(
            sql,
            new
            {
                request.OrganizationId,
                WeekStart = request.WeekStart.ToDateTime(TimeOnly.MinValue),
                request.Weeks
            },
            cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
