using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.OrganizationRole.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetOrganizationRoles
{
    public sealed record GetOrganizationRolesQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<OrganizationRoleDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationRolesQueryHandler
        : IRequestHandler<GetOrganizationRolesQuery, IReadOnlyList<OrganizationRoleDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationRolesQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<OrganizationRoleDto>> Handle(
            GetOrganizationRolesQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"             AS "Id",
                    "OrganizationId" AS "OrganizationId",
                    "Name"           AS "Name",
                    "Description"    AS "Description"
                FROM "OrganizationRoles"
                WHERE "OrganizationId" = @OrganizationId
                  AND "IsDeleted" = FALSE
                ORDER BY "Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var roles =
                await connection.QueryAsync<OrganizationRoleDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return roles.ToList();
        }
    }
}
