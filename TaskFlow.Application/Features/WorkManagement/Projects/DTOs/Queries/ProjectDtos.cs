using TaskFlow.Domain.Enums.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Projects.DTOs.Queries
{
    public sealed class ProjectDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public ProjectStatus Status { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? ExpectedCompletionDate { get; init; }
        public DateTime? ActualCompletionDate { get; init; }
        public int CreatedByUserId { get; init; }
        public int TaskCount { get; init; }
        public int CompletedTaskCount { get; init; }
        public decimal CompletionPercentage { get; init; }
    }
}
