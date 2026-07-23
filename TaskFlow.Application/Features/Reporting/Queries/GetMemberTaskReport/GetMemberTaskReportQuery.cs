using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetMemberTaskReport
{
    /// <summary>
    /// Task throughput and tracked time for one member over a
    /// window. Pick the window (week/month/year) via From/To.
    /// </summary>
    public sealed record GetMemberTaskReportQuery(
        int UserId,
        DateTime From,
        DateTime To
    ) : IRequest<MemberTaskReportDto>;

    public sealed class GetMemberTaskReportQueryHandler
        : IRequestHandler<GetMemberTaskReportQuery, MemberTaskReportDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetMemberTaskReportQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<MemberTaskReportDto> Handle(
            GetMemberTaskReportQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    u."Id"                               AS "UserId",
                    u."FirstName" || ' ' || u."LastName" AS "FullName",
                    CAST(@From AS timestamp with time zone) AS "From",
                    CAST(@To   AS timestamp with time zone) AS "To",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."CreatedAt" BETWEEN @From AND @To)
                        AS "TasksCreated",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."AssignedToUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."CreatedAt" BETWEEN @From AND @To)
                        AS "TasksAssigned",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."AssignedToUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."Status" = 3
                       AND t."ActualCompletionDate" BETWEEN @From AND @To)
                        AS "TasksCompleted",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."AssignedToUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."Status" = 2)
                        AS "TasksInProgress",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."AssignedToUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."Status" <> 3
                       AND t."ExpectedCompletionDate" IS NOT NULL
                       AND t."ExpectedCompletionDate" < NOW())
                        AS "TasksOverdue",
                    (SELECT COALESCE(SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0), 0)
                     FROM "TaskWorkLogs" wl
                     WHERE wl."UserId" = u."Id" AND wl."IsDeleted" = FALSE
                       AND wl."StartedAt" BETWEEN @From AND @To)
                        AS "TrackedHours"
                FROM "Users" u
                WHERE u."Id" = @UserId AND u."IsDeleted" = FALSE;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var report =
                await connection.QuerySingleOrDefaultAsync<MemberTaskReportDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            request.UserId,
                            From = DateTime.SpecifyKind(request.From, DateTimeKind.Utc),
                            To = DateTime.SpecifyKind(request.To, DateTimeKind.Utc)
                        },
                        cancellationToken: cancellationToken));

            if (report is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            return report;
        }
    }
}
