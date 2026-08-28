using TaskFlow.Domain.Enums.Planner;

namespace TaskFlow.Domain.Entities.Planner;

public sealed class RequirementSnapshot
{
    public Guid Id { get; private set; }
    public Guid BaselineId { get; private set; }
    public RequirementEntityType EntityType { get; private set; }
    public int EntityId { get; private set; }
    public int? ParentEntityId { get; private set; }
    public int OrderIndex { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FieldsJson { get; private set; } = "{}";
    public DateTime CapturedAt { get; private set; }
    public RequirementBaseline Baseline { get; private set; } = null!;

    private RequirementSnapshot()
    {
    }

    public RequirementSnapshot(
        Guid baselineId,
        RequirementEntityType entityType,
        int entityId,
        int? parentEntityId,
        int orderIndex,
        string title,
        string fieldsJson)
    {
        if (baselineId == Guid.Empty) throw new ArgumentException("Baseline id is required.", nameof(baselineId));
        if (entityId <= 0) throw new ArgumentOutOfRangeException(nameof(entityId));
        if (orderIndex < 0) throw new ArgumentOutOfRangeException(nameof(orderIndex));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(fieldsJson)) throw new ArgumentException("Fields are required.", nameof(fieldsJson));

        Id = Guid.NewGuid();
        BaselineId = baselineId;
        EntityType = entityType;
        EntityId = entityId;
        ParentEntityId = parentEntityId;
        OrderIndex = orderIndex;
        Title = title.Trim();
        FieldsJson = fieldsJson;
        CapturedAt = DateTime.UtcNow;
    }
}
