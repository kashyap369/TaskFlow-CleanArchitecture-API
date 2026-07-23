using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.Team.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.Team.Queries.GetOrganizationTeams
{
    public sealed record GetOrganizationTeamsQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<TeamDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationTeamsQueryHandler
        : IRequestHandler<GetOrganizationTeamsQuery, IReadOnlyList<TeamDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationTeamsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<TeamDto>> Handle(
            GetOrganizationTeamsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    t."Id"             AS "Id",
                    t."OrganizationId" AS "OrganizationId",
                    t."Name"           AS "Name",
                    t."Description"    AS "Description",
                    (
                        SELECT COUNT(*)
                        FROM "TeamMembers" tm
                        WHERE tm."TeamId" = t."Id"
                          AND tm."IsActive" = TRUE
                          AND tm."IsDeleted" = FALSE
                    )                  AS "MemberCount"
                FROM "Teams" t
                WHERE t."OrganizationId" = @OrganizationId
                  AND t."IsDeleted" = FALSE
                ORDER BY t."Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var teams =
                await connection.QueryAsync<TeamDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return teams.ToList();
        }
    }
}
