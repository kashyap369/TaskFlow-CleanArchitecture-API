using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Organizations.OrganizationInvitation.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.Queries.GetMyInvitations
{
    /// <summary>
    /// Pending invitations addressed to the current user's email.
    /// </summary>
    public sealed record GetMyInvitationsQuery
        : IRequest<IReadOnlyList<OrganizationInvitationDto>>;

    public sealed class GetMyInvitationsQueryHandler
        : IRequestHandler<GetMyInvitationsQuery, IReadOnlyList<OrganizationInvitationDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyInvitationsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<OrganizationInvitationDto>> Handle(
            GetMyInvitationsQuery request,
            CancellationToken cancellationToken)
        {
            // Status 1 = Pending.
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
                WHERE LOWER(i."Email") = LOWER(@Email)
                  AND i."Status" = 1
                  AND i."IsDeleted" = FALSE
                ORDER BY i."CreatedAt" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var invitations =
                await connection.QueryAsync<OrganizationInvitationDto>(
                    new CommandDefinition(
                        sql,
                        new { Email = _currentUserService.Email },
                        cancellationToken: cancellationToken));

            return invitations.ToList();
        }
    }
}
