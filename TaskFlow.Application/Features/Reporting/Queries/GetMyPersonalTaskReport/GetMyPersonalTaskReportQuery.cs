using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Reporting.DTOs;

namespace TaskFlow.Application.Features.Reporting.Queries.GetMyPersonalTaskReport
{
    /// <summary>
    /// Personal tracking report for the signed-in user (Individual account):
    /// tasks created / completed / in progress / overdue and tracked hours
    /// across their <b>personal</b> tasks over a window. Pick the window
    /// (weekly / monthly / yearly) via From/To.
    ///
    /// Deliberately carries no UserId and no access-scope marker: it always
    /// reports on the caller, taken from the JWT, so there is nothing to
    /// authorize and no id to tamper with.
    /// </summary>
    public sealed record GetMyPersonalTaskReportQuery(
        DateTime From,
        DateTime To
    ) : IRequest<PersonalTaskReportDto>;

    public sealed class GetMyPersonalTaskReportQueryHandler
        : IRequestHandler<GetMyPersonalTaskReportQuery, PersonalTaskReportDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyPersonalTaskReportQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<PersonalTaskReportDto> Handle(
            GetMyPersonalTaskReportQuery request,
            CancellationToken cancellationToken)
        {
            // Personal tasks only: "OrganizationId" IS NULL, created by the
            // caller. Work logs are joined through those tasks so org time
            // never leaks into a personal report.
            const string sql = """
                SELECT
                    u."Id"                               AS "UserId",
                    u."FirstName" || ' ' || u."LastName" AS "FullName",
                    CAST(@From AS timestamp with time zone) AS "From",
                    CAST(@To   AS timestamp with time zone) AS "To",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND t."CreatedAt" BETWEEN @From AND @To)
                        AS "TasksCreated",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND t."Status" = 3
                       AND t."ActualCompletionDate" BETWEEN @From AND @To)
                        AS "TasksCompleted",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND t."Status" = 2)
                        AS "TasksInProgress",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND t."Status" = 1)
                        AS "TasksTodo",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."CreatedByUserId" = u."Id" AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND t."Status" <> 3
                       AND t."ExpectedCompletionDate" IS NOT NULL
                       AND t."ExpectedCompletionDate" < NOW())
                        AS "TasksOverdue",
                    (SELECT COALESCE(SUM(
                            EXTRACT(EPOCH FROM (COALESCE(wl."EndedAt", NOW()) - wl."StartedAt")) / 3600.0), 0)
                     FROM "TaskWorkLogs" wl
                     INNER JOIN "Tasks" t ON t."Id" = wl."TaskId"
                     WHERE wl."UserId" = u."Id" AND wl."IsDeleted" = FALSE
                       AND t."IsDeleted" = FALSE
                       AND t."OrganizationId" IS NULL
                       AND wl."StartedAt" BETWEEN @From AND @To)
                        AS "TrackedHours"
                FROM "Users" u
                WHERE u."Id" = @UserId AND u."IsDeleted" = FALSE;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var report =
                await connection.QuerySingleOrDefaultAsync<PersonalTaskReportDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            UserId = _currentUserService.UserId,
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
