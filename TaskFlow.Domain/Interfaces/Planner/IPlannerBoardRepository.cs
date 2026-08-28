using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Domain.Interfaces.Planner;

public interface IPlannerBoardRepository
{
    Task<PlannerBoard?> GetByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<PlannerBoard?> GetSceneByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlannerSceneRevision>> GetRevisionsAsync(
        Guid boardId,
        CancellationToken cancellationToken = default);

    Task<PlannerSceneRevision?> GetRevisionAsync(
        Guid boardId,
        int revisionNumber,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PlannerBoard board,
        CancellationToken cancellationToken = default);

    Task AddRevisionAsync(
        PlannerSceneRevision revision,
        CancellationToken cancellationToken = default);

    Task PruneRevisionsAsync(
        Guid boardId,
        int currentRevision,
        int retentionLimit,
        CancellationToken cancellationToken = default);

    void Update(PlannerBoard board);
}
