namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.DTOs.Queries
{
    public sealed class WorkLogDto
    {
        public int Id { get; init; }
        public int TaskId { get; init; }
        public int UserId { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime? EndedAt { get; init; }
        public double DurationMinutes { get; init; }
        public string? Notes { get; init; }
        public bool IsRunning { get; init; }
    }
}
