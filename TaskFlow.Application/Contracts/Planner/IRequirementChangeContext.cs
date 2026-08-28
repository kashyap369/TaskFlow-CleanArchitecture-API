namespace TaskFlow.Application.Contracts.Planner;

public interface IRequirementChangeContext
{
    string? Reason { get; }
    void SetReason(string? reason);
}

public sealed class RequirementChangeContext : IRequirementChangeContext
{
    public string? Reason { get; private set; }

    public void SetReason(string? reason)
    {
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
