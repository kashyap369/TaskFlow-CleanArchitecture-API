using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Planner;

public sealed class PlannerBoardRepository : IPlannerBoardRepository
{
    private readonly TaskFlowDbContext _context;

    public PlannerBoardRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public Task<PlannerBoard?> GetByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        _context.PlannerBoards
            .Include(x => x.Nodes)
            .ThenInclude(x => x.TemplateVersion)
            .Include(x => x.Nodes)
            .ThenInclude(x => x.Resource)
            .ThenInclude(x => x!.Asset)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public Task<PlannerBoard?> GetSceneByProjectIdAsync(
        int projectId,
        CancellationToken cancellationToken = default) =>
        _context.PlannerBoards
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public async Task<IReadOnlyList<PlannerSceneRevision>> GetRevisionsAsync(
        Guid boardId,
        CancellationToken cancellationToken = default) =>
        await _context.PlannerSceneRevisions
            .AsNoTracking()
            .Where(x => x.BoardId == boardId)
            .OrderByDescending(x => x.RevisionNumber)
            .ToListAsync(cancellationToken);

    public Task<PlannerSceneRevision?> GetRevisionAsync(
        Guid boardId,
        int revisionNumber,
        CancellationToken cancellationToken = default) =>
        _context.PlannerSceneRevisions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BoardId == boardId && x.RevisionNumber == revisionNumber,
                cancellationToken);

    public Task AddAsync(
        PlannerBoard board,
        CancellationToken cancellationToken = default) =>
        _context.PlannerBoards.AddAsync(board, cancellationToken).AsTask();

    public Task AddRevisionAsync(
        PlannerSceneRevision revision,
        CancellationToken cancellationToken = default) =>
        _context.PlannerSceneRevisions.AddAsync(revision, cancellationToken).AsTask();

    public async Task PruneRevisionsAsync(
        Guid boardId,
        int currentRevision,
        int retentionLimit,
        CancellationToken cancellationToken = default)
    {
        if (retentionLimit < 1) throw new ArgumentOutOfRangeException(nameof(retentionLimit));
        var oldestRevisionToKeep = currentRevision - retentionLimit + 1;
        if (oldestRevisionToKeep <= 1) return;

        await _context.PlannerSceneRevisions
            .Where(x => x.BoardId == boardId && x.RevisionNumber < oldestRevisionToKeep)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void Update(PlannerBoard board)
    {
        // The board is normally loaded as a tracked aggregate. Mark only its root as modified;
        // DbSet.Update would treat newly-created UUID nodes as existing rows and issue an UPDATE,
        // which becomes a false optimistic-concurrency conflict when zero rows exist yet.
        var newNodes = board.Nodes
            .Where(node => _context.Entry(node).State is EntityState.Detached or EntityState.Modified)
            .ToList();
        _context.Entry(board).State = EntityState.Modified;
        foreach (var node in newNodes)
        {
            _context.Entry(node).State = EntityState.Added;
        }
    }
}
