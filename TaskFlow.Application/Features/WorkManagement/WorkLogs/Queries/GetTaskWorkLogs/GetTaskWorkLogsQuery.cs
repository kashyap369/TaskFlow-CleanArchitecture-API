using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.WorkLogs.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Queries.GetTaskWorkLogs
{
    public sealed record GetTaskWorkLogsQuery(
        int TaskId
    ) : IRequest<IReadOnlyList<WorkLogDto>>;

    public sealed class GetTaskWorkLogsQueryHandler
        : IRequestHandler<GetTaskWorkLogsQuery, IReadOnlyList<WorkLogDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTaskWorkLogsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<WorkLogDto>> Handle(
            GetTaskWorkLogsQuery request,
            CancellationToken cancellationToken)
        {
            // A running log (EndedAt null) counts elapsed time
            // up to now; IsRunning tells the client it is live.
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
                WHERE "TaskId" = @TaskId
                  AND "IsDeleted" = FALSE
                ORDER BY "StartedAt" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var logs =
                await connection.QueryAsync<WorkLogDto>(
                    new CommandDefinition(
                        sql,
                        new { request.TaskId },
                        cancellationToken: cancellationToken));

            return logs.ToList();
        }
    }
}
