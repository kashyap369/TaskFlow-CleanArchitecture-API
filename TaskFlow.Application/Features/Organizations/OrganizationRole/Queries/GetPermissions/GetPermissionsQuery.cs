using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.OrganizationRole.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetPermissions
{
    /// <summary>
    /// The organization permission catalog — the fixed set of
    /// permissions that can be granted to organization roles.
    /// </summary>
    public sealed record GetPermissionsQuery
        : IRequest<IReadOnlyList<OrganizationPermissionDto>>;

    public sealed class GetPermissionsQueryHandler
        : IRequestHandler<GetPermissionsQuery, IReadOnlyList<OrganizationPermissionDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetPermissionsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<OrganizationPermissionDto>> Handle(
            GetPermissionsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    "Id"          AS "Id",
                    "Name"        AS "Name",
                    "Description" AS "Description"
                FROM "OrganizationPermissions"
                WHERE "IsDeleted" = FALSE
                ORDER BY "Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var permissions =
                await connection.QueryAsync<OrganizationPermissionDto>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken));

            return permissions.ToList();
        }
    }
}
