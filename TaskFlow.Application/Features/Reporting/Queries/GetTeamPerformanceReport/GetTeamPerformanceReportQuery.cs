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

            // The tasks each team explicitly owns (Task.TeamId, added
            // in Phase 11) — the "which tasks" the OVERVIEW asks for.
            // Fetched for the whole organization in one round trip and
            // grouped in memory, rather than one query per team.
            const string tasksSql = """
                SELECT
                    tk."TeamId"                AS "TeamId",
                    tk."Id"                    AS "TaskId",
                    tk."Title"                 AS "Title",
                    tk."Status"                AS "Status",
                    tk."Priority"              AS "Priority",
                    tk."StartDate"             AS "StartDate",
                    tk."ActualCompletionDate"  AS "ActualCompletionDate",
                    tk."AssignedToUserId"      AS "AssignedToUserId",
                    CASE
                        WHEN u."Id" IS NULL THEN NULL
                        ELSE u."FirstName" || ' ' || u."LastName"
                    END                        AS "AssignedToFullName",
                    COALESCE((
                        SELECT SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0)
                        FROM "TaskWorkLogs" wl
                        WHERE wl."TaskId" = tk."Id" AND wl."IsDeleted" = FALSE), 0)
                        AS "TrackedHours"
                FROM "Tasks" tk
                JOIN "Teams" t
                    ON t."Id" = tk."TeamId" AND t."IsDeleted" = FALSE
                LEFT JOIN "Users" u
                    ON u."Id" = tk."AssignedToUserId"
                WHERE t."OrganizationId" = @OrganizationId
                  AND tk."IsDeleted" = FALSE
                  AND tk."StartDate" BETWEEN @From AND @To
                ORDER BY tk."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var parameters = new
            {
                request.OrganizationId,
                From = DateTime.SpecifyKind(request.From, DateTimeKind.Utc),
                To = DateTime.SpecifyKind(request.To, DateTimeKind.Utc)
            };

            var rows =
                (await connection.QueryAsync<TeamPerformanceReportDto>(
                    new CommandDefinition(
                        sql,
                        parameters,
                        cancellationToken: cancellationToken)))
                .ToList();

            var taskRows =
                await connection.QueryAsync<TeamTaskRow>(
                    new CommandDefinition(
                        tasksSql,
                        parameters,
                        cancellationToken: cancellationToken));

            var tasksByTeam =
                taskRows
                    .GroupBy(x => x.TeamId)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<TeamTaskReportItemDto>)g
                            .Select(x => new TeamTaskReportItemDto
                            {
                                TaskId = x.TaskId,
                                Title = x.Title,
                                Status = x.Status,
                                Priority = x.Priority,
                                StartDate = x.StartDate,
                                ActualCompletionDate = x.ActualCompletionDate,
                                AssignedToUserId = x.AssignedToUserId,
                                AssignedToFullName = x.AssignedToFullName,
                                TrackedHours = x.TrackedHours
                            })
                            .ToList());

            foreach (var row in rows)
            {
                if (tasksByTeam.TryGetValue(row.TeamId, out var tasks))
                {
                    row.Tasks = tasks;
                }
            }

            return rows;
        }

        /// <summary>
        /// Flat Dapper row for the per-team task list. Carries
        /// <c>TeamId</c> so the rows can be grouped back onto their
        /// team; the shipped DTO does not need it.
        /// </summary>
        private sealed class TeamTaskRow
        {
            public int TeamId { get; init; }
            public int TaskId { get; init; }
            public string Title { get; init; } = string.Empty;
            public int Status { get; init; }
            public int Priority { get; init; }
            public DateTime StartDate { get; init; }
            public DateTime? ActualCompletionDate { get; init; }
            public int? AssignedToUserId { get; init; }
            public string? AssignedToFullName { get; init; }
            public double TrackedHours { get; init; }
        }
    }
}
