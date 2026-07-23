using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetTeamPerformanceReport
{
    /// <summary>
    /// One performance row per team in the organization over a
    /// window: assignment/completion counts, tracked hours and
    /// average completion time, based on tasks assigned to the
    /// team's active members.
    /// </summary>
    public sealed record GetTeamPerformanceReportQuery(
        int OrganizationId,
        DateTime From,
        DateTime To
    ) : IRequest<IReadOnlyList<TeamPerformanceReportDto>>, IOrganizationScopedRequest;

    public sealed class GetTeamPerformanceReportQueryHandler
        : IRequestHandler<GetTeamPerformanceReportQuery, IReadOnlyList<TeamPerformanceReportDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTeamPerformanceReportQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<TeamPerformanceReportDto>> Handle(
            GetTeamPerformanceReportQuery request,
            CancellationToken cancellationToken)
        {
            // "team members" = active TeamMembers of the team.
            const string sql = """
                SELECT
                    t."Id"   AS "TeamId",
                    t."Name" AS "TeamName",
                    (SELECT COUNT(*) FROM "TeamMembers" tm
                     WHERE tm."TeamId" = t."Id" AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE)
                        AS "ActiveMembers",
                    (SELECT COUNT(*) FROM "Tasks" tk
                     WHERE tk."IsDeleted" = FALSE
                       AND tk."CreatedAt" BETWEEN @From AND @To
                       AND tk."AssignedToUserId" IN (
                           SELECT tm."UserId" FROM "TeamMembers" tm
                           WHERE tm."TeamId" = t."Id" AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE))
                        AS "TasksAssigned",
                    (SELECT COUNT(*) FROM "Tasks" tk
                     WHERE tk."IsDeleted" = FALSE AND tk."Status" = 3
                       AND tk."ActualCompletionDate" BETWEEN @From AND @To
                       AND tk."AssignedToUserId" IN (
                           SELECT tm."UserId" FROM "TeamMembers" tm
                           WHERE tm."TeamId" = t."Id" AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE))
                        AS "TasksCompleted",
                    (SELECT COALESCE(SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0), 0)
                     FROM "TaskWorkLogs" wl
                     WHERE wl."IsDeleted" = FALSE
                       AND wl."StartedAt" BETWEEN @From AND @To
                       AND wl."UserId" IN (
                           SELECT tm."UserId" FROM "TeamMembers" tm
                           WHERE tm."TeamId" = t."Id" AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE))
                        AS "TrackedHours",
                    (SELECT COALESCE(AVG(
                            EXTRACT(EPOCH FROM (tk."ActualCompletionDate" - tk."StartDate")) / 86400.0), 0)
                     FROM "Tasks" tk
                     WHERE tk."IsDeleted" = FALSE AND tk."Status" = 3
                       AND tk."ActualCompletionDate" BETWEEN @From AND @To
                       AND tk."AssignedToUserId" IN (
                           SELECT tm."UserId" FROM "TeamMembers" tm
                           WHERE tm."TeamId" = t."Id" AND tm."IsActive" = TRUE AND tm."IsDeleted" = FALSE))
                        AS "AvgCompletionDays"
                FROM "Teams" t
                WHERE t."OrganizationId" = @OrganizationId AND t."IsDeleted" = FALSE
                ORDER BY t."Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var rows =
                await connection.QueryAsync<TeamPerformanceReportDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            request.OrganizationId,
                            From = DateTime.SpecifyKind(request.From, DateTimeKind.Utc),
                            To = DateTime.SpecifyKind(request.To, DateTimeKind.Utc)
                        },
                        cancellationToken: cancellationToken));

            return rows.ToList();
        }
    }
}
