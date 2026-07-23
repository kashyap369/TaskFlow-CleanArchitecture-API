using TaskFlow.Application.Features.WorkManagement.SubTasks.DTOs.Queries;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.DTOs.Queries
{
    public sealed class TaskListItemDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public TaskPriority Priority { get; init; }
        public TaskStatus Status { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? ExpectedCompletionDate { get; init; }
        public DateTime? ActualCompletionDate { get; init; }
        public int? ProjectId { get; init; }
        public int? OrganizationId { get; init; }
        public int CreatedByUserId { get; init; }
        public int? AssignedToUserId { get; init; }
        public int SubTaskCount { get; init; }
        public int CompletedSubTaskCount { get; init; }
    }

    public sealed class TaskDetailDto
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public TaskPriority Priority { get; init; }
        public TaskStatus Status { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? ExpectedCompletionDate { get; init; }
        public DateTime? ActualCompletionDate { get; init; }
        public int? ProjectId { get; init; }
        public int? OrganizationId { get; init; }
        public int CreatedByUserId { get; init; }
        public int? AssignedToUserId { get; init; }
        public string? AssignedToFullName { get; init; }
        public IReadOnlyList<SubTaskDto> SubTasks { get; set; } =
            Array.Empty<SubTaskDto>();
    }
}
