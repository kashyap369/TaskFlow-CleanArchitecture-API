using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.OrganizationMember.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Queries.GetOrganizationMembers
{
    public sealed record GetOrganizationMembersQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<OrganizationMemberDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationMembersQueryHandler
        : IRequestHandler<GetOrganizationMembersQuery, IReadOnlyList<OrganizationMemberDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationMembersQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<OrganizationMemberDto>> Handle(
            GetOrganizationMembersQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    m."Id"                            AS "Id",
                    m."OrganizationId"                AS "OrganizationId",
                    m."UserId"                        AS "UserId",
                    u."FirstName" || ' ' || u."LastName" AS "UserFullName",
                    u."Email"                         AS "Email",
                    m."OrganizationRoleId"            AS "OrganizationRoleId",
                    r."Name"                          AS "RoleName",
                    m."IsActive"                      AS "IsActive",
                    m."JoinedAt"                      AS "JoinedAt"
                FROM "OrganizationMembers" m
                JOIN "Users" u
                    ON u."Id" = m."UserId"
                JOIN "OrganizationRoles" r
                    ON r."Id" = m."OrganizationRoleId"
                WHERE m."OrganizationId" = @OrganizationId
                  AND m."IsDeleted" = FALSE
                ORDER BY u."FirstName", u."LastName";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var members =
                await connection.QueryAsync<OrganizationMemberDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return members.ToList();
        }
    }
}
