using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Organizations.Organization.DTOs.Queries;

namespace TaskFlow.Application.Features.Organizations.Organization.Queries.GetOrganizationById
{
    public sealed record GetOrganizationByIdQuery(
        int OrganizationId
    ) : IRequest<OrganizationDetailDto>;

    public sealed class GetOrganizationByIdQueryHandler
        : IRequestHandler<GetOrganizationByIdQuery, OrganizationDetailDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<OrganizationDetailDto> Handle(
            GetOrganizationByIdQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    o."Id"          AS "Id",
                    o."Name"        AS "Name",
                    o."Description" AS "Description",
                    o."OwnerUserId" AS "OwnerUserId",
                    o."Status"      AS "Status",
                    o."CreatedAt"   AS "CreatedAt",
                    (
                        SELECT COUNT(*)
                        FROM "OrganizationMembers" m
                        WHERE m."OrganizationId" = o."Id"
                          AND m."IsActive" = TRUE
                          AND m."IsDeleted" = FALSE
                    )               AS "MemberCount"
                FROM "Organizations" o
                WHERE o."Id" = @OrganizationId
                  AND o."IsDeleted" = FALSE;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var organization =
                await connection.QuerySingleOrDefaultAsync<OrganizationDetailDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            if (organization is null)
            {
                throw new NotFoundException(
                    "ORGANIZATION_NOT_FOUND",
                    "Organization not found.");
            }

            return organization;
        }
    }
}
