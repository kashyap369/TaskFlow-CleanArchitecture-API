using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.Organization.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.Organization.Queries.GetAllOrganizations
{
    /// <summary>
    /// Every organization on the platform, for the admin portal.
    ///
    /// Deliberately carries <b>no access-scope marker</b>: the markers
    /// in <c>Common/Authorization</c> resolve a request to one
    /// organization and demand owner/member access, which is exactly
    /// the wrong question here — an admin belongs to no organization.
    /// Authorization is the <c>AdminOnly</c> policy on the route, the
    /// same way <c>GET /user</c> already works.
    /// </summary>
    public sealed record GetAllOrganizationsQuery
        : IRequest<IReadOnlyList<AdminOrganizationListItemDto>>;

    public sealed class GetAllOrganizationsQueryHandler
        : IRequestHandler<GetAllOrganizationsQuery,
            IReadOnlyList<AdminOrganizationListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetAllOrganizationsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<AdminOrganizationListItemDto>> Handle(
            GetAllOrganizationsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    o."Id"          AS "Id",
                    o."Name"        AS "Name",
                    o."Description" AS "Description",
                    o."OwnerUserId" AS "OwnerUserId",
                    u."FirstName" || ' ' || u."LastName" AS "OwnerFullName",
                    u."Email"       AS "OwnerEmail",
                    o."Status"      AS "Status",
                    o."CreatedAt"   AS "CreatedAt",
                    (SELECT COUNT(*) FROM "OrganizationMembers" m
                     WHERE m."OrganizationId" = o."Id"
                       AND m."IsActive" = TRUE
                       AND m."IsDeleted" = FALSE)
                        AS "MemberCount",
                    (SELECT COUNT(*) FROM "Projects" p
                     WHERE p."OrganizationId" = o."Id"
                       AND p."IsDeleted" = FALSE)
                        AS "ProjectCount",
                    (SELECT COUNT(*) FROM "Tasks" t
                     WHERE t."OrganizationId" = o."Id"
                       AND t."IsDeleted" = FALSE)
                        AS "TaskCount"
                FROM "Organizations" o
                LEFT JOIN "Users" u
                    ON u."Id" = o."OwnerUserId"
                WHERE o."IsDeleted" = FALSE
                ORDER BY o."Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var organizations =
                await connection.QueryAsync<AdminOrganizationListItemDto>(
                    new CommandDefinition(
                        sql,
                        cancellationToken: cancellationToken));

            return organizations.ToList();
        }
    }
}
