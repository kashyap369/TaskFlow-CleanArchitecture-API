namespace TaskFlow.Application.Features.WorkManagement.Tasks.Queries
{
    /// <summary>
    /// Shared SELECT for the task list queries. Each query
    /// appends its own WHERE/ORDER BY. Sub-task counts come from
    /// correlated sub-queries; status 3 = Completed.
    /// </summary>
    internal static class TaskListSql
    {
        public const string Select = """
            SELECT
                t."Id"                     AS "Id",
                t."Title"                  AS "Title",
                t."Priority"               AS "Priority",
                t."Status"                 AS "Status",
                t."StartDate"              AS "StartDate",
                t."ExpectedCompletionDate" AS "ExpectedCompletionDate",
                t."ActualCompletionDate"   AS "ActualCompletionDate",
                t."ProjectId"              AS "ProjectId",
                t."OrganizationId"         AS "OrganizationId",
                t."CreatedByUserId"        AS "CreatedByUserId",
                t."AssignedToUserId"       AS "AssignedToUserId",
                (
                    SELECT COUNT(*) FROM "SubTasks" s
                    WHERE s."TaskId" = t."Id" AND s."IsDeleted" = FALSE
                )                          AS "SubTaskCount",
                (
                    SELECT COUNT(*) FROM "SubTasks" s
                    WHERE s."TaskId" = t."Id" AND s."IsDeleted" = FALSE
                      AND s."Status" = 3
                )                          AS "CompletedSubTaskCount"
            FROM "Tasks" t
            """;
    }
}
