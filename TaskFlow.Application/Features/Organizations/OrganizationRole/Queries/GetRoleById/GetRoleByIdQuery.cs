using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Organizations.OrganizationRole.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationRole.Queries.GetRoleById
{
    public sealed record GetRoleByIdQuery(
        int OrganizationRoleId
    ) : IRequest<OrganizationRoleDetailDto>, IRoleScopedRequest;

    public sealed class GetRoleByIdQueryHandler
        : IRequestHandler<GetRoleByIdQuery, OrganizationRoleDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetRoleByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<OrganizationRoleDetailDto> Handle(
            GetRoleByIdQuery request,
            CancellationToken cancellationToken)
        {
            const string roleSql = """
                SELECT
                    "Id"             AS "Id",
                    "OrganizationId" AS "OrganizationId",
                    "Name"           AS "Name",
                    "Description"    AS "Description"
                FROM "OrganizationRoles"
                WHERE "Id" = @OrganizationRoleId
                  AND "IsDeleted" = FALSE;
                """;

            const string permissionsSql = """
                SELECT p."Name"
                FROM "OrganizationRolePermissions" rp
                JOIN "OrganizationPermissions" p
                    ON p."Id" = rp."OrganizationPermissionId"
                WHERE rp."OrganizationRoleId" = @OrganizationRoleId
                  AND rp."IsDeleted" = FALSE
                  AND p."IsDeleted" = FALSE
                ORDER BY p."Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var role =
                await connection.QuerySingleOrDefaultAsync<OrganizationRoleDetailDto>(
                    new CommandDefinition(
                        roleSql,
                        new { request.OrganizationRoleId },
                        cancellationToken: cancellationToken));

            if (role is null)
            {
                throw new NotFoundException(
                    "ROLE_NOT_FOUND",
                    "Organization role not found.");
            }

            var permissions =
                await connection.QueryAsync<string>(
                    new CommandDefinition(
                        permissionsSql,
                        new { request.OrganizationRoleId },
                        cancellationToken: cancellationToken));

            role.Permissions = permissions.ToList();

            return role;
        }
    }
}
