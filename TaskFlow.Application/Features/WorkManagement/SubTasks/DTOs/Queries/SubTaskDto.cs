using TaskStatus = TaskFlow.Domain.Enums.WorkManagement.TaskStatus;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.DTOs.Queries
{
    public sealed class SubTaskDto
    {
        public int Id { get; init; }
        public int TaskId { get; init; }
        public string Title { get; init; } = string.Empty;
        public TaskStatus Status { get; init; }
        public DateTime CreatedDate { get; init; }
        public DateTime? CompletedDate { get; init; }
    }
}
