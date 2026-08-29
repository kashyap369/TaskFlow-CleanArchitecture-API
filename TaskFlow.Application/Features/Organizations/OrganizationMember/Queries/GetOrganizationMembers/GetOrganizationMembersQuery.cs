using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.OrganizationMember.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationMember.Queries.GetOrganizationMembers
{
    /// <summary>
    /// The organization's members. The two filters are optional and
    /// default to "no filter", so every existing caller and every
    /// existing client keeps the old behaviour.
    ///
    /// <para><b>OrganizationRoleId</b> backs the OVERVIEW promise that
    /// assignment can be "optionally filtered role-wise" — e.g. a
    /// Manager assigning a design task lists only members in the
    /// Designer role. It is a filter on the candidate list, not a
    /// restriction on who may be assigned: the assign command still
    /// accepts any active member, which is what "optionally" means.</para>
    ///
    /// <para><b>ActiveOnly</b> is what an assignee picker actually
    /// wants — an inactive member should not appear as a candidate.</para>
    /// </summary>
    public sealed record GetOrganizationMembersQuery(
        int OrganizationId,
        int? OrganizationRoleId = null,
        bool ActiveOnly = false
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
            // Both filters are applied in SQL rather than by
            // appending strings: a NULL parameter disables its own
            // clause, so the statement text is constant and stays
            // parameterised.
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
                    ,m."WeeklyCapacityMinutes"        AS "WeeklyCapacityMinutes"
                FROM "OrganizationMembers" m
                JOIN "Users" u
                    ON u."Id" = m."UserId"
                JOIN "OrganizationRoles" r
                    ON r."Id" = m."OrganizationRoleId"
                WHERE m."OrganizationId" = @OrganizationId
                  AND m."IsDeleted" = FALSE
                  AND (@OrganizationRoleId IS NULL
                       OR m."OrganizationRoleId" = @OrganizationRoleId)
                  AND (@ActiveOnly = FALSE
                       OR m."IsActive" = TRUE)
                ORDER BY u."FirstName", u."LastName";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var members =
                await connection.QueryAsync<OrganizationMemberDto>(
                    new CommandDefinition(
                        sql,
                        new
                        {
                            request.OrganizationId,
                            request.OrganizationRoleId,
                            request.ActiveOnly
                        },
                        cancellationToken: cancellationToken));

            return members.ToList();
        }
    }
}
