using Dapper;
using MediatR;
using TaskFlow.Application.Contracts.Persistence;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.Projects.DTOs.Queries;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Queries.GetMyPersonalProjects;

public sealed record GetMyPersonalProjectsQuery
    : IRequest<IReadOnlyList<ProjectDto>>;

public sealed class GetMyPersonalProjectsQueryHandler
    : IRequestHandler<GetMyPersonalProjectsQuery, IReadOnlyList<ProjectDto>>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;
    private readonly ICurrentUserService _currentUserService;

    public GetMyPersonalProjectsQueryHandler(
        ISqlConnectionFactory sqlConnectionFactory,
        ICurrentUserService currentUserService)
    {
        _sqlConnectionFactory = sqlConnectionFactory;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<ProjectDto>> Handle(
        GetMyPersonalProjectsQuery request,
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
            WHERE p."OrganizationId" IS NULL
              AND p."CreatedByUserId" = @UserId
              AND p."IsDeleted" = FALSE
            GROUP BY p."Id"
            ORDER BY p."StartDate" DESC;
            """;

        using var connection = _sqlConnectionFactory.Create();
        var projects = await connection.QueryAsync<ProjectDto>(
            new CommandDefinition(
                sql,
                new { UserId = _currentUserService.UserId },
                cancellationToken: cancellationToken));

        return projects.ToList();
    }
}
