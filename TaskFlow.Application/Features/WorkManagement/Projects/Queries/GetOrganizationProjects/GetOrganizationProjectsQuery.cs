using Dapper;
using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Features.WorkManagement.Projects.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetOrganizationProjects
{
    public sealed record GetOrganizationProjectsQuery(
        int OrganizationId
    ) : IRequest<IReadOnlyList<ProjectDto>>, IOrganizationScopedRequest;

    public sealed class GetOrganizationProjectsQueryHandler
        : IRequestHandler<GetOrganizationProjectsQuery, IReadOnlyList<ProjectDto>>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetOrganizationProjectsQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<IReadOnlyList<ProjectDto>> Handle(
            GetOrganizationProjectsQuery request,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    p."Id"                     AS "Id",
                    p."OrganizationId"         AS "OrganizationId",
                    p."Title"                  AS "Title",
                    p."Description"            AS "Description",
                    p."Status"                 AS "Status",
                    p."StartDate"              AS "StartDate",
                    p."ExpectedCompletionDate" AS "ExpectedCompletionDate",
                    p."ActualCompletionDate"   AS "ActualCompletionDate",
                    p."CreatedByUserId"        AS "CreatedByUserId",
                    COUNT(t."Id")                                AS "TaskCount",
                    COUNT(t."Id") FILTER (WHERE t."Status" = 3) AS "CompletedTaskCount",
                    CASE
                        WHEN COUNT(t."Id") = 0 THEN 0
                        ELSE ROUND(
                            COUNT(t."Id") FILTER (WHERE t."Status" = 3)::decimal
                            / COUNT(t."Id") * 100, 2)
                    END                                          AS "CompletionPercentage"
                FROM "Projects" p
                LEFT JOIN "Tasks" t
                    ON t."ProjectId" = p."Id"
                   AND t."IsDeleted" = FALSE
                WHERE p."OrganizationId" = @OrganizationId
                  AND p."IsDeleted" = FALSE
                GROUP BY p."Id"
                ORDER BY p."StartDate" DESC;
                """;

            using var connection = _sqlConnectionFactory.Create();

            var projects =
                await connection.QueryAsync<ProjectDto>(
                    new CommandDefinition(
                        sql,
                        new { request.OrganizationId },
                        cancellationToken: cancellationToken));

            return projects.ToList();
        }
    }
}
