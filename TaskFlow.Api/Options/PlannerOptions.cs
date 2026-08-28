namespace TaskFlow.Api.Options;

public sealed class PlannerOptions
{
    public const string SectionName = "Planner";

    public bool Enabled { get; init; } = true;
    public int SlowRequestMilliseconds { get; init; } = 1_500;
}
