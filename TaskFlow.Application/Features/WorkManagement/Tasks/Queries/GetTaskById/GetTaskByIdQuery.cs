using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.WorkManagement.SubTasks.DTOs.Queries;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetTaskById
{
    public sealed record GetTaskByIdQuery(
        int TaskId
    ) : IRequest<TaskDetailDto>;

    public sealed class GetTaskByIdQueryHandler
        : IRequestHandler<GetTaskByIdQuery, TaskDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTaskByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<TaskDetailDto> Handle(
            GetTaskByIdQuery request,
            CancellationToken cancellationToken)
        {
            const string taskSql = """
                SELECT
                    t."Id"                     AS "Id",
                    t."Title"                  AS "Title",
                    t."Description"            AS "Description",
                    t."Priority"               AS "Priority",
                    t."Status"                 AS "Status",
                    t."StartDate"              AS "StartDate",
                    t."ExpectedCompletionDate" AS "ExpectedCompletionDate",
                    t."ActualCompletionDate"   AS "ActualCompletionDate",
                    t."ProjectId"              AS "ProjectId",
                    t."OrganizationId"         AS "OrganizationId",
                    t."CreatedByUserId"        AS "CreatedByUserId",
                    t."AssignedToUserId"       AS "AssignedToUserId",
                    CASE
                        WHEN a."Id" IS NULL THEN NULL
                        ELSE a."FirstName" || ' ' || a."LastName"
                    END                        AS "AssignedToFullName"
                FROM "Tasks" t
                LEFT JOIN "Users" a
                    ON a."Id" = t."AssignedToUserId"
                WHERE t."Id" = @TaskId
                  AND t."IsDeleted" = FALSE;
                """;

            const string subTasksSql = """
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

            var task =
                await connection.QuerySingleOrDefaultAsync<TaskDetailDto>(
                    new CommandDefinition(
                        taskSql,
                        new { request.TaskId },
                        cancellationToken: cancellationToken));

            if (task is null)
            {
                throw new NotFoundException(
                    "TASK_NOT_FOUND",
                    "Task not found.");
            }

            var subTasks =
                await connection.QueryAsync<SubTaskDto>(
                    new CommandDefinition(
                        subTasksSql,
                        new { request.TaskId },
                        cancellationToken: cancellationToken));

            task.SubTasks = subTasks.ToList();

            return task;
        }
    }
}
