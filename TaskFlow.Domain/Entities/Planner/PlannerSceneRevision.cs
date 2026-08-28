namespace TaskFlow.Domain.Entities.Planner;

public sealed class PlannerSceneRevision
{
    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public int RevisionNumber { get; private set; }
    public string SceneJson { get; private set; } = string.Empty;
    public int CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private PlannerSceneRevision()
    {
    }

    internal PlannerSceneRevision(
        Guid boardId,
        int revisionNumber,
        string sceneJson,
        int createdByUserId)
    {
        if (boardId == Guid.Empty)
            throw new ArgumentException("Board id is required.", nameof(boardId));
        if (revisionNumber <= 0)
            throw new ArgumentException("Revision number must be positive.", nameof(revisionNumber));
        if (createdByUserId <= 0)
            throw new ArgumentException("Creator id is required.", nameof(createdByUserId));

        PlannerSceneDocument.EnsureValid(sceneJson);

        Id = Guid.NewGuid();
        BoardId = boardId;
        RevisionNumber = revisionNumber;
        SceneJson = sceneJson;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }
}
