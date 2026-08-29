namespace TaskFlow.Application.Features.Reporting.DTOs;

public sealed class CapacityDto
{
    public int OrganizationId { get; init; }
    public int UserId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public DateOnly WeekStart { get; init; }
    public DateOnly WeekEnd { get; init; }
    public int? WeeklyCapacityMinutes { get; init; }
    public int? AssignedEstimateMinutes { get; init; }
    public int? RemainingCapacityMinutes { get; init; }
    public int AssignedTaskCount { get; init; }
    public int MissingEstimateTaskCount { get; init; }
    public bool HasEnoughData { get; init; }
    public string WorkloadState { get; init; } = "NotEnoughData";
}
