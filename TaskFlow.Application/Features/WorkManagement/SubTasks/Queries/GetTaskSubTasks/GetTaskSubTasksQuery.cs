using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.SubTasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Queries.GetTaskSubTasks
{
    public sealed record GetTaskSubTasksQuery(
        int TaskId
    ) : IRequest<IReadOnlyList<SubTaskDto>>;

    public sealed class GetTaskSubTasksQueryHandler
        : IRequestHandler<GetTaskSubTasksQuery, IReadOnlyList<SubTaskDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTaskSubTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<SubTaskDto>> Handle(
            GetTaskSubTasksQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"            AS "Id",
                    "TaskId"        AS "TaskId",
                    "Title"         AS "Title",
                    "Status"        AS "Status",
                    "CreatedDate"   AS "CreatedDate",
                    "CompletedDate" AS "CompletedDate"
                FROM "SubTasks"
                WHERE "TaskId" = @TaskId
                  AND "IsDeleted" = FALSE
                ORDER BY "CreatedDate";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var subTasks =
                await connection.QueryAsync<SubTaskDto>(
                    new CommandDefinition(
                        sql,
                        new { request.TaskId },
                        cancellationToken: cancellationToken));

            return subTasks.ToList();
        }
    }
}
