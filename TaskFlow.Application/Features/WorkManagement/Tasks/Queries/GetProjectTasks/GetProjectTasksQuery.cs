using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetProjectTasks
{
    public sealed record GetProjectTasksQuery(
        int ProjectId
    ) : IRequest<IReadOnlyList<TaskListItemDto>>, IProjectScopedRequest;

    public sealed class GetProjectTasksQueryHandler
        : IRequestHandler<GetProjectTasksQuery, IReadOnlyList<TaskListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetProjectTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<TaskListItemDto>> Handle(
            GetProjectTasksQuery request,
            CancellationToken cancellationToken)
        {
            var sql = TaskListSql.Select + """

                WHERE t."ProjectId" = @ProjectId
                  AND t."IsDeleted" = FALSE
                ORDER BY t."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var tasks =
                await connection.QueryAsync<TaskListItemDto>(
                    new CommandDefinition(
                        sql,
                        new { request.ProjectId },
                        cancellationToken: cancellationToken));

            return tasks.ToList();
        }
    }
}
