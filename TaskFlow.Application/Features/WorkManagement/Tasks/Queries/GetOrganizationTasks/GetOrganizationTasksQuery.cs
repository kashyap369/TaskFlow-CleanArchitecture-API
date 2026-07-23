using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetOrganizationTasks
{
    public sealed record GetOrganizationTasksQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<TaskListItemDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationTasksQueryHandler
        : IRequestHandler<GetOrganizationTasksQuery, IReadOnlyList<TaskListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<TaskListItemDto>> Handle(
            GetOrganizationTasksQuery request,
            CancellationToken cancellationToken)
        {
            var sql = TaskListSql.Select + """

                WHERE t."OrganizationId" = @OrganizationId
                  AND t."IsDeleted" = FALSE
                ORDER BY t."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var tasks =
                await connection.QueryAsync<TaskListItemDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return tasks.ToList();
        }
    }
}
