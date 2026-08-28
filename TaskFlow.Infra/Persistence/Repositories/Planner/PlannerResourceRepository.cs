using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Planner;

public sealed class PlannerResourceRepository : IPlannerResourceRepository
{
    private readonly TaskFlowDbContext _context;
    public PlannerResourceRepository(TaskFlowDbContext context) => _context = context;

    public Task<PlannerResource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default) =>
        _context.PlannerResources.Include(x => x.Asset)
            .FirstOrDefaultAsync(x => x.Id == resourceId, cancellationToken);

    public async Task<IReadOnlyList<PlannerResource>> ListAsync(Guid boardId,
        CancellationToken cancellationToken = default) =>
        await _context.PlannerResources.AsNoTracking().Include(x => x.Asset)
            .Where(x => x.BoardId == boardId).OrderBy(x => x.CreatedAt).ToListAsync(cancellationToken);

    public Task AddAsync(PlannerResource resource, CancellationToken cancellationToken = default) =>
        _context.PlannerResources.AddAsync(resource, cancellationToken).AsTask();

    public void Remove(PlannerResource resource) => _context.PlannerResources.Remove(resource);
}
