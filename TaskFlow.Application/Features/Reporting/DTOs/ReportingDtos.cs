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

        // Priority breakdown. OVERVIEW promises task reports
        // "by status/priority" — status was covered, priority was
        // in no reporting DTO at all. Enum: 1 Low, 2 Medium,
        // 3 High, 4 Critical.
        public int LowPriorityTasks { get; init; }
        public int MediumPriorityTasks { get; init; }
        public int HighPriorityTasks { get; init; }
        public int CriticalPriorityTasks { get; init; }
    }

    /// <summary>
    /// Personal tracking report for an Individual account: the caller's own
    /// <b>personal</b> tasks (no organization) over a date window. Has no
    /// "assigned" count — personal tasks cannot be assigned to anyone.
    /// </summary>
    public sealed class PersonalTaskReportDto
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public DateTime From { get; init; }
        public DateTime To { get; init; }
        public int TasksCreated { get; init; }
        public int TasksCompleted { get; init; }
        public int TasksInProgress { get; init; }
        public int TasksTodo { get; init; }
        public int TasksOverdue { get; init; }
        public double TrackedHours { get; init; }
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

    /// <summary>
    /// One row per task a team owns — the "<b>which tasks</b>" half of
    /// the OVERVIEW promise "which team performed which tasks, and in
    /// what duration". The report used to give counts and an average
    /// only, never the tasks themselves.
    /// </summary>
    public sealed class TeamTaskReportItemDto
    {
        public int TaskId { get; init; }
        public string Title { get; init; } = string.Empty;
        public int Status { get; init; }
        public int Priority { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime? ActualCompletionDate { get; init; }
        public int? AssignedToUserId { get; init; }
        public string? AssignedToFullName { get; init; }
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

        /// <summary>
        /// The tasks explicitly owned by this team (<c>Task.TeamId</c>),
        /// within the report window. Empty for a team that owns no
        /// tasks — the aggregate counts above are still based on the
        /// team's <i>members</i>, so the two can legitimately differ.
        /// </summary>
        public IReadOnlyList<TeamTaskReportItemDto> Tasks { get; set; } =
            Array.Empty<TeamTaskReportItemDto>();
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

        // Timeline. OVERVIEW promises project reports covering
        // "progress, workload, timeline" — progress and workload were
        // covered, but the DTO carried no dates at all. These are the
        // project's own planned window plus the actual span of its
        // tasks, so a client can render planned-vs-actual from one
        // call. Names mirror the Project entity exactly.
        public DateTime StartDate { get; init; }
        public DateTime? ExpectedCompletionDate { get; init; }
        public DateTime? ActualCompletionDate { get; init; }
        public DateTime? FirstTaskStartDate { get; init; }
        public DateTime? LastTaskCompletionDate { get; init; }

        public IReadOnlyList<ProjectMemberWorkloadDto> MemberWorkloads { get; set; } =
            Array.Empty<ProjectMemberWorkloadDto>();
    }
}
