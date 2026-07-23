namespace TaskFlow.Application.Features.Reporting.DTOs
{
    /// <summary>Org-wide counts for the dashboard header.</summary>
    public sealed class DashboardSummaryDto
    {
        public int OrganizationId { get; init; }
        public int ProjectCount { get; init; }
        public int MemberCount { get; init; }
        public int TeamCount { get; init; }
        public int TotalTasks { get; init; }
        public int TodoTasks { get; init; }
        public int InProgressTasks { get; init; }
        public int CompletedTasks { get; init; }
        public int OverdueTasks { get; init; }
        public int UnassignedTasks { get; init; }
        public double TotalTrackedHours { get; init; }
    }

    /// <summary>
    /// Task throughput and tracked time for a single member over
    /// a date window (weekly/monthly/yearly by choosing From/To).
    /// </summary>
    public sealed class MemberTaskReportDto
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public int TasksCreated { get; init; }
        public int TasksAssigned { get; init; }
        public int TasksCompleted { get; init; }
        public int TasksInProgress { get; init; }
        public int TasksOverdue { get; init; }
        public double TrackedHours { get; init; }
    }

    /// <summary>One team's aggregate performance over a window.</summary>
    public sealed class TeamPerformanceReportDto
    {
        public int TeamId { get; init; }
        public string TeamName { get; init; } = string.Empty;
        public int ActiveMembers { get; init; }
        public int TasksAssigned { get; init; }
        public int TasksCompleted { get; init; }
        public double TrackedHours { get; init; }
        public double AvgCompletionDays { get; init; }
    }

    /// <summary>Per-member workload breakdown inside a project.</summary>
    public sealed class ProjectMemberWorkloadDto
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public int TasksAssigned { get; init; }
        public int TasksCompleted { get; init; }
        public double TrackedHours { get; init; }
    }

    public sealed class ProjectReportDto
    {
        public int ProjectId { get; init; }
        public string Title { get; init; } = string.Empty;
        public int TotalTasks { get; init; }
        public int CompletedTasks { get; init; }
        public decimal CompletionPercentage { get; init; }
        public double TrackedHours { get; init; }
        public IReadOnlyList<ProjectMemberWorkloadDto> MemberWorkloads { get; set; } =
            Array.Empty<ProjectMemberWorkloadDto>();
    }
}
