using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Queries.GetMyWorkLogs
{
    /// <summary>
    /// The current user's work logs whose start falls in the
    /// [From, To] window.
    /// </summary>
    public sealed record GetMyWorkLogsQuery(
        DateTime From,
        DateTime To
    ) : IRequest<IReadOnlyList<WorkLogDto>>;

    public sealed class GetMyWorkLogsQueryHandler
        : IRequestHandler<GetMyWorkLogsQuery, IReadOnlyList<WorkLogDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyWorkLogsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<WorkLogDto>> Handle(
            GetMyWorkLogsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"       AS "Id",
                    "TaskId"   AS "TaskId",
                    "UserId"   AS "UserId",
                    "StartedAt" AS "StartedAt",
                    "EndedAt"  AS "EndedAt",
                    EXTRACT(EPOCH FROM (
                        COALESCE("EndedAt", NOW()) - "StartedAt"
                    )) / 60.0  AS "DurationMinutes",
                    "Notes"    AS "Notes",
                    ("EndedAt" IS NULL) AS "IsRunning"
                FROM "TaskWorkLogs"
                WHERE "UserId" = @UserId
                  AND "StartedAt" >= @From
                  AND "StartedAt" <= @To
                  AND "IsDeleted" = FALSE
                ORDER BY "StartedAt" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var logs =
                await connection.QueryAsync<WorkLogDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            UserId = _currentUserService.UserId,
                            // timestamptz columns require UTC-kind values.
                            From = DateTime.SpecifyKind(
                                request.From, DateTimeKind.Utc),
                            To = DateTime.SpecifyKind(
                                request.To, DateTimeKind.Utc)
                        },
                        cancellationToken: cancellationToken));

            return logs.ToList();
        }
    }
}
