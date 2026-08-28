namespace TaskFlow.Domain.Enums.Planner;

public enum PlannerResourceKind
{
    Note = 1,
    Link = 2,
    Document = 3
}

public enum PlannerAssetScanStatus
{
    Pending = 1,
    Clean = 2,
    Rejected = 3,
    Failed = 4
}
