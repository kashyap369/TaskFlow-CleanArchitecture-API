using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Organizations.Team.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.Team.Queries.GetTeamById
{
    public sealed record GetTeamByIdQuery(
        int TeamId
    ) : IRequest<TeamDetailDto>;

    public sealed class GetTeamByIdQueryHandler
        : IRequestHandler<GetTeamByIdQuery, TeamDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetTeamByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<TeamDetailDto> Handle(
            GetTeamByIdQuery request,
            CancellationToken cancellationToken)
        {
            const string teamSql = """
                SELECT
                    "Id"             AS "Id",
                    "OrganizationId" AS "OrganizationId",
                    "Name"           AS "Name",
                    "Description"    AS "Description"
                FROM "Teams"
                WHERE "Id" = @TeamId
                  AND "IsDeleted" = FALSE;
                """;

            const string membersSql = """
                SELECT
                    tm."UserId"                       AS "UserId",
                    u."FirstName" || ' ' || u."LastName" AS "FullName",
                    u."Email"                         AS "Email",
                    tm."JoinedAt"                     AS "JoinedAt"
                FROM "TeamMembers" tm
                JOIN "Users" u
                    ON u."Id" = tm."UserId"
                WHERE tm."TeamId" = @TeamId
                  AND tm."IsActive" = TRUE
                  AND tm."IsDeleted" = FALSE
                ORDER BY u."FirstName", u."LastName";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var team =
                await connection.QuerySingleOrDefaultAsync<TeamDetailDto>(
                    new CommandDefinition(
                        teamSql,
                        new { request.TeamId },
                        cancellationToken: cancellationToken));

            if (team is null)
            {
                throw new NotFoundException(
                    "TEAM_NOT_FOUND",
                    "Team not found.");
            }

            var members =
                await connection.QueryAsync<TeamMemberDto>(
                    new CommandDefinition(
                        membersSql,
                        new { request.TeamId },
                        cancellationToken: cancellationToken));

            team.Members = members.ToList();

            return team;
        }
    }
}
