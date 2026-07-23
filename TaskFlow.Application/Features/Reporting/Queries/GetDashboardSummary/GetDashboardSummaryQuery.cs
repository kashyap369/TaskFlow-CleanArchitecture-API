using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetDashboardSummary
{
    public sealed record GetDashboardSummaryQuery(
        int OrganizationId
    ) : IRequest<DashboardSummaryDto>;

    public sealed class GetDashboardSummaryQueryHandler
        : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetDashboardSummaryQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<DashboardSummaryDto> Handle(
            GetDashboardSummaryQuery request,
            CancellationToken cancellationToken)
        {
            // Task status codes: 1 Todo, 2 InProgress, 3 Completed.
            const string sql = """
                SELECT
                    CAST(@OrganizationId AS int) AS "OrganizationId",
                    (SELECT COUNT(*) FROM "Projects"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE)
                        AS "ProjectCount",
                    (SELECT COUNT(*) FROM "OrganizationMembers"
                     WHERE "OrganizationId" = @OrganizationId AND "IsActive" = TRUE AND "IsDeleted" = FALSE)
                        AS "MemberCount",
                    (SELECT COUNT(*) FROM "Teams"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE)
                        AS "TeamCount",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE)
                        AS "TotalTasks",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE AND "Status" = 1)
                        AS "TodoTasks",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE AND "Status" = 2)
                        AS "InProgressTasks",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE AND "Status" = 3)
                        AS "CompletedTasks",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE
                       AND "Status" <> 3
                       AND "ExpectedCompletionDate" IS NOT NULL
                       AND "ExpectedCompletionDate" < NOW())
                        AS "OverdueTasks",
                    (SELECT COUNT(*) FROM "Tasks"
                     WHERE "OrganizationId" = @OrganizationId AND "IsDeleted" = FALSE
                       AND "Status" <> 3 AND "AssignedToUserId" IS NULL)
                        AS "UnassignedTasks",
                    (SELECT COALESCE(SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0), 0)
                     FROM "TaskWorkLogs" wl
                     JOIN "Tasks" t ON t."Id" = wl."TaskId"
                     WHERE t."OrganizationId" = @OrganizationId AND wl."IsDeleted" = FALSE)
                        AS "TotalTrackedHours";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var summary =
                await connection.QuerySingleAsync<DashboardSummaryDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return summary;
        }
    }
}
