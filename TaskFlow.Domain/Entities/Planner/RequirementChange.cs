using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class RequirementChange
{
    public Guid Id { get; private set; }
    public Guid BaselineId { get; private set; }
    public RequirementEntityType EntityType { get; private set; }
    public int EntityId { get; private set; }
    public int? ParentEntityId { get; private set; }
    public RequirementChangeType ChangeType { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? OldValuesJson { get; private set; }
    public string? NewValuesJson { get; private set; }
    public int ActorUserId { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string? Reason { get; private set; }
    public RequirementBaseline Baseline { get; private set; } = null!;

    private RequirementChange()
    {
    }

    public RequirementChange(
        Guid baselineId,
        RequirementEntityType entityType,
        int entityId,
        int? parentEntityId,
        RequirementChangeType changeType,
        string title,
        string? oldValuesJson,
        string? newValuesJson,
        int actorUserId,
        string? reason)
    {
        if (baselineId == Guid.Empty) throw new ArgumentException("Baseline id is required.", nameof(baselineId));
        if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
        if (actorUserId <= 0) throw new ArgumentOutOfRangeException(nameof(actorUserId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (reason?.Length > 500) throw new ArgumentException("Reason cannot exceed 500 characters.", nameof(reason));

        Id = Guid.NewGuid();
        BaselineId = baselineId;
        EntityType = entityType;
        EntityId = entityId;
        ParentEntityId = parentEntityId;
        ChangeType = changeType;
        Title = title.Trim();
        OldValuesJson = oldValuesJson;
        NewValuesJson = newValuesJson;
        ActorUserId = actorUserId;
        ChangedAt = DateTime.UtcNow;
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }
}
