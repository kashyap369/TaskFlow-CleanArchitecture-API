using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Queries.GetOrganizationInvitations
{
    public sealed record GetOrganizationInvitationsQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<OrganizationInvitationDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationInvitationsQueryHandler
        : IRequestHandler<GetOrganizationInvitationsQuery, IReadOnlyList<OrganizationInvitationDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationInvitationsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<OrganizationInvitationDto>> Handle(
            GetOrganizationInvitationsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    i."Id"                 AS "Id",
                    i."OrganizationId"     AS "OrganizationId",
                    o."Name"               AS "OrganizationName",
                    i."Email"              AS "Email",
                    i."OrganizationRoleId" AS "OrganizationRoleId",
                    r."Name"               AS "RoleName",
                    i."Status"             AS "Status",
                    i."ExpiryDate"         AS "ExpiryDate",
                    i."CreatedAt"          AS "CreatedAt"
                FROM "OrganizationInvitations" i
                JOIN "Organizations" o
                    ON o."Id" = i."OrganizationId"
                JOIN "OrganizationRoles" r
                    ON r."Id" = i."OrganizationRoleId"
                WHERE i."OrganizationId" = @OrganizationId
                  AND i."IsDeleted" = FALSE
                ORDER BY i."CreatedAt" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var invitations =
                await connection.QueryAsync<OrganizationInvitationDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return invitations.ToList();
        }
    }
}
