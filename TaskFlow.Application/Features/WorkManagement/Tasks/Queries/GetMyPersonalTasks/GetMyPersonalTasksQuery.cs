using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetMyPersonalTasks
{
    /// <summary>
    /// The current user's personal tasks (no organization),
    /// for the Individual-account workspace.
    /// </summary>
    public sealed record GetMyPersonalTasksQuery
        : IRequest<IReadOnlyList<TaskListItemDto>>;

    public sealed class GetMyPersonalTasksQueryHandler
        : IRequestHandler<GetMyPersonalTasksQuery, IReadOnlyList<TaskListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyPersonalTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<TaskListItemDto>> Handle(
            GetMyPersonalTasksQuery request,
            CancellationToken cancellationToken)
        {
            var sql = TaskListSql.Select + """

                WHERE t."OrganizationId" IS NULL
                  AND t."CreatedByUserId" = @UserId
                  AND t."IsDeleted" = FALSE
                ORDER BY t."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var tasks =
                await connection.QueryAsync<TaskListItemDto>(
                    new CommandDefinition(
                        sql,
                        new { UserId = _currentUserService.UserId },
                        cancellationToken: cancellationToken));

            return tasks.ToList();
        }
    }
}
