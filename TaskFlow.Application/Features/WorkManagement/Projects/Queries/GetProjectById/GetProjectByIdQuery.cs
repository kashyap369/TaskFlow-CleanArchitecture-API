using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.WorkManagement.Projects.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetProjectById
{
    public sealed record GetProjectByIdQuery(
        int ProjectId
    ) : IRequest<ProjectDto>;

    public sealed class GetProjectByIdQueryHandler
        : IRequestHandler<GetProjectByIdQuery, ProjectDto>
    {
        private readonly ISqlConnectionFactory _sqlConnectionFactory;

        public GetProjectByIdQueryHandler(
            ISqlConnectionFactory sqlConnectionFactory)
        {
            _sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<ProjectDto> Handle(
            GetProjectByIdQuery request,
            CancellationToken cancellationToken)
        {
            // Status 3 = Completed (TaskStatus). Completion % is
            // derived from the project's task rows.
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
                    COUNT(t."Id")                                          AS "TaskCount",
                    COUNT(t."Id") FILTER (WHERE t."Status" = 3)           AS "CompletedTaskCount",
                    CASE
                        WHEN COUNT(t."Id") = 0 THEN 0
                        ELSE ROUND(
                            COUNT(t."Id") FILTER (WHERE t."Status" = 3)::decimal
                            / COUNT(t."Id") * 100, 2)
                    END                                                    AS "CompletionPercentage"
                FROM "Projects" p
                LEFT JOIN "Tasks" t
                    ON t."ProjectId" = p."Id"
                   AND t."IsDeleted" = FALSE
                WHERE p."Id" = @ProjectId
                  AND p."IsDeleted" = FALSE
                GROUP BY p."Id";
                """;

            using var connection = _sqlConnectionFactory.Create();

            var project =
                await connection.QuerySingleOrDefaultAsync<ProjectDto>(
                    new CommandDefinition(
                        sql,
                        new { request.ProjectId },
                        cancellationToken: cancellationToken));

            if (project is null)
            {
                throw new NotFoundException(
                    "PROJECT_NOT_FOUND",
                    "Project not found.");
            }

            return project;
        }
    }
}
