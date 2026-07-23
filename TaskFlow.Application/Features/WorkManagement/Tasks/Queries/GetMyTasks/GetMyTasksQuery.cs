using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetMyTasks
{
    /// <summary>
    /// Organization tasks assigned to the current user.
    /// </summary>
    public sealed record GetMyTasksQuery
        : IRequest<IReadOnlyList<TaskListItemDto>>;

    public sealed class GetMyTasksQueryHandler
        : IRequestHandler<GetMyTasksQuery, IReadOnlyList<TaskListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<TaskListItemDto>> Handle(
            GetMyTasksQuery request,
            CancellationToken cancellationToken)
        {
            var sql = TaskListSql.Select + """

                WHERE t."AssignedToUserId" = @UserId
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
