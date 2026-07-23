using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetProjectReport
{
    /// <summary>
    /// Progress, tracked time and per-member workload for one
    /// project.
    /// </summary>
    public sealed record GetProjectReportQuery(
        int ProjectId
    ) : IRequest<ProjectReportDto>;

    public sealed class GetProjectReportQueryHandler
        : IRequestHandler<GetProjectReportQuery, ProjectReportDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetProjectReportQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<ProjectReportDto> Handle(
            GetProjectReportQuery request,
            CancellationToken cancellationToken)
        {
            const string projectSql = """
                SELECT
                    p."Id"    AS "ProjectId",
                    p."Title" AS "Title",
                    COUNT(t."Id")                                AS "TotalTasks",
                    COUNT(t."Id") FILTER (WHERE t."Status" = 3) AS "CompletedTasks",
                    CASE
                        WHEN COUNT(t."Id") = 0 THEN 0
                        ELSE ROUND(
                            COUNT(t."Id") FILTER (WHERE t."Status" = 3)::decimal
                            / COUNT(t."Id") * 100, 2)
                    END                                          AS "CompletionPercentage",
                    (SELECT COALESCE(SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0), 0)
                     FROM "TaskWorkLogs" wl
                     JOIN "Tasks" t2 ON t2."Id" = wl."TaskId"
                     WHERE t2."ProjectId" = p."Id" AND wl."IsDeleted" = FALSE)
                        AS "TrackedHours"
                FROM "Projects" p
                LEFT JOIN "Tasks" t
                    ON t."ProjectId" = p."Id" AND t."IsDeleted" = FALSE
                WHERE p."Id" = @ProjectId AND p."IsDeleted" = FALSE
                GROUP BY p."Id", p."Title";
                """;

            const string workloadSql = """
                SELECT
                    t."AssignedToUserId"                 AS "UserId",
                    u."FirstName" || ' ' || u."LastName" AS "FullName",
                    COUNT(*)                                AS "TasksAssigned",
                    COUNT(*) FILTER (WHERE t."Status" = 3) AS "TasksCompleted",
                    COALESCE((
                        SELECT SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0)
                        FROM "TaskWorkLogs" wl
                        JOIN "Tasks" t2 ON t2."Id" = wl."TaskId"
                        WHERE t2."ProjectId" = @ProjectId
                          AND wl."UserId" = t."AssignedToUserId"
                          AND wl."IsDeleted" = FALSE), 0)
                        AS "TrackedHours"
                FROM "Tasks" t
                JOIN "Users" u ON u."Id" = t."AssignedToUserId"
                WHERE t."ProjectId" = @ProjectId
                  AND t."IsDeleted" = FALSE
                  AND t."AssignedToUserId" IS NOT NULL
                GROUP BY t."AssignedToUserId", u."FirstName", u."LastName"
                ORDER BY "TasksAssigned" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var report =
                await connection.QuerySingleOrDefaultAsync<ProjectReportDto>(
                    new CommandDefinition(
                        projectSql,
                        new { request.ProjectId },
                        cancellationToken: cancellationToken));

            if (report is null)
            {
                throw new NotFoundException(
                    "PROJECT_NOT_FOUND",
                    "Project not found.");
            }

            var workloads =
                await connection.QueryAsync<ProjectMemberWorkloadDto>(
                    new CommandDefinition(
                        workloadSql,
                        new { request.ProjectId },
                        cancellationToken: cancellationToken));

            report.MemberWorkloads = workloads.ToList();

            return report;
        }
    }
}
