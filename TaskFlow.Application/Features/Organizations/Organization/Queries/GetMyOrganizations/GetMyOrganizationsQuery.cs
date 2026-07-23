using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Organizations.Organization.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.Organization.Queries.GetMyOrganizations
{
    public sealed record GetMyOrganizationsQuery
        : IRequest<IReadOnlyList<OrganizationListItemDto>>;

    public sealed class GetMyOrganizationsQueryHandler
        : IRequestHandler<GetMyOrganizationsQuery, IReadOnlyList<OrganizationListItemDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;
        private readonly ICurrentUserService _currentUserService;

        public GetMyOrganizationsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory,
            ICurrentUserService currentUserService)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
            _currentUserService = currentUserService;
        }

        public async Task<IReadOnlyList<OrganizationListItemDto>> Handle(
            GetMyOrganizationsQuery request,
            CancellationToken cancellationToken)
        {
            // Organizations the user owns or is an active member of.
            const string sql = """
                SELECT
                    o."Id"          AS "Id",
                    o."Name"        AS "Name",
                    o."OwnerUserId" AS "OwnerUserId",
                    o."Status"      AS "Status"
                FROM "Organizations" o
                WHERE o."IsDeleted" = FALSE
                  AND (
                        o."OwnerUserId" = @UserId
                        OR EXISTS (
                            SELECT 1
                            FROM "OrganizationMembers" m
                            WHERE m."OrganizationId" = o."Id"
                              AND m."UserId" = @UserId
                              AND m."IsActive" = TRUE
                              AND m."IsDeleted" = FALSE
                        )
                      )
                ORDER BY o."Name";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var organizations =
                await connection.QueryAsync<OrganizationListItemDto>(
                    new CommandDefinition(
                        sql,
                        new { UserId = _currentUserService.UserId },
                        cancellationToken: cancellationToken));

            return organizations.ToList();
        }
    }
}
