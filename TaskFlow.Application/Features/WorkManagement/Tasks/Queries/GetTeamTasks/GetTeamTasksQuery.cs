using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries.GetTeamTasks
{
    /// <summary>
    /// The tasks a team is responsible for. This is what makes the
    /// OVERVIEW promise "tasks and reports can be viewed per team"
    /// true — before Phase 11 teams grouped only people, and there
    /// was no way to ask which work belonged to one.
    ///
    /// Marked <see cref="ITeamScopedRequest"/> so
    /// <c>AccessGuardBehavior</c> resolves the team to its
    /// organization and enforces owner/active-member access before
    /// the handler runs, exactly like every other team read.
    /// </summary>
    public sealed record GetTeamTasksQuery(
        int TeamId
    ) : IRequest<IReadOnlyList<TaskListItemDto>>, ITeamScopedRequest;

    public sealed class GetTeamTasksQueryHandler
        : IRequestHandler<GetTeamTasksQuery, IReadOnlyList<TaskListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTeamTasksQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<TaskListItemDto>> Handle(
            GetTeamTasksQuery request,
            CancellationToken cancellationToken)
        {
            var sql = TaskListSql.Select + """

                WHERE t."TeamId" = @TeamId
                  AND t."IsDeleted" = FALSE
                ORDER BY t."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var tasks =
                await connection.QueryAsync<TaskListItemDto>(
                    new CommandDefinition(
                        sql,
                        new { request.TeamId },
                        cancellationToken: cancellationToken));

            return tasks.ToList();
        }
    }
}
