using System.Text.Json;
using ProjectEntity = TaskFlow.Domain.Entities.WorkManagement.Projects.Project;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
using SubTaskEntity = TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask;

namespace TaskFlow.Application.Features.Planner;

public static class RequirementFields
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string ForProject(ProjectEntity project) => Serialize(new Dictionary<string, object?>
    {
        ["title"] = project.Title,
        ["description"] = project.Description,
        ["expectedCompletionDate"] = project.ExpectedCompletionDate,
        ["problemStatement"] = project.ProblemStatement,
        ["budgetAmount"] = project.BudgetAmount,
        ["budgetCurrency"] = project.BudgetCurrency,
        ["approximateDurationWeeks"] = project.ApproximateDurationWeeks,
    });

    public static string ForTask(TaskEntity task) => Serialize(new Dictionary<string, object?>
    {
        ["title"] = task.Title,
        ["description"] = task.Description,
        ["priority"] = (int)task.Priority,
        ["expectedCompletionDate"] = task.ExpectedCompletionDate,
    });

    public static string ForSubTask(SubTaskEntity subTask) => Serialize(new Dictionary<string, object?>
    {
        ["title"] = subTask.Title,
    });

    public static string Serialize(IReadOnlyDictionary<string, object?> fields) =>
        JsonSerializer.Serialize(fields, Options);
}
