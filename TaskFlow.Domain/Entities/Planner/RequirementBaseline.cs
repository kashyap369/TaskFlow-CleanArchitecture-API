namespace TaskFlow.Domain.Entities.Planner;

public sealed class RequirementBaseline
{
    private readonly List<RequirementSnapshot> _snapshots = new();

    public Guid Id { get; private set; }
    public Guid BoardId { get; private set; }
    public int ProjectId { get; private set; }
    public int BaselineNumber { get; private set; }
    public int FinalizedByUserId { get; private set; }
    public DateTime FinalizedAt { get; private set; }
    public PlannerBoard Board { get; private set; } = null!;
    public IReadOnlyCollection<RequirementSnapshot> Snapshots => _snapshots.AsReadOnly();

    private RequirementBaseline()
    {
    }

    private RequirementBaseline(PlannerBoard board, int baselineNumber, int finalizedByUserId)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (baselineNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(baselineNumber));
        if (finalizedByUserId <= 0 || finalizedByUserId != board.OwnerUserId)
            throw new ArgumentException("Only the Planner owner can finalize a baseline.");

        Id = Guid.NewGuid();
        Board = board;
        BoardId = board.Id;
        ProjectId = board.ProjectId;
        BaselineNumber = baselineNumber;
        FinalizedByUserId = finalizedByUserId;
        FinalizedAt = DateTime.UtcNow;
    }

    public static RequirementBaseline Create(
        PlannerBoard board,
        int baselineNumber,
        int finalizedByUserId) =>
        new(board, baselineNumber, finalizedByUserId);

    public void AddSnapshot(RequirementSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.BaselineId != Id)
            throw new InvalidOperationException("Snapshot belongs to another baseline.");
        if (_snapshots.Any(x => x.EntityType == snapshot.EntityType && x.EntityId == snapshot.EntityId))
            throw new InvalidOperationException("A requirement can be captured only once per baseline.");

        _snapshots.Add(snapshot);
    }
}
