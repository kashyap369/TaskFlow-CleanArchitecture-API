using MediatR;
using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.ImportProjectPlan;

public sealed record ImportProjectPlanCommand(
    string Title,
    string Description,
    DateTime StartDate,
    DateTime? ExpectedCompletionDate,
    int? OrganizationId,
    IReadOnlyList<ImportProjectPlanTask> Tasks
) : IRequest<ImportProjectPlanResult>;

public sealed record ImportProjectPlanTask(
    string Key,
    string Title,
    string Description,
    DateTime StartDate,
    DateTime? ExpectedCompletionDate,
    TaskPriority Priority,
    int? EstimateMinutes,
    string? TeamName,
    string? AssigneeEmail,
    IReadOnlyList<string> SubTasks);

public sealed record ImportProjectPlanResult(
    int ProjectId,
    int TaskCount,
    int SubTaskCount);
