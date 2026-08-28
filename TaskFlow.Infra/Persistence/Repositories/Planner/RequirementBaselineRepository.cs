using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Planner;

public sealed class RequirementBaselineRepository : IRequirementBaselineRepository
{
    private readonly TaskFlowDbContext _context;

    public RequirementBaselineRepository(TaskFlowDbContext context)
    {
        _context = context;
    }

    public Task<RequirementBaseline?> GetLatestAsync(int projectId, CancellationToken cancellationToken = default) =>
        _context.RequirementBaselines.Include(x => x.Snapshots)
            .OrderByDescending(x => x.BaselineNumber)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId, cancellationToken);

    public Task<RequirementBaseline?> GetByIdAsync(int projectId, Guid baselineId, CancellationToken cancellationToken = default) =>
        _context.RequirementBaselines.AsNoTracking().Include(x => x.Snapshots)
            .FirstOrDefaultAsync(x => x.ProjectId == projectId && x.Id == baselineId, cancellationToken);

    public async Task<IReadOnlyList<RequirementBaseline>> GetAllAsync(int projectId, CancellationToken cancellationToken = default) =>
        await _context.RequirementBaselines.AsNoTracking().Include(x => x.Snapshots).Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.BaselineNumber).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RequirementChange>> GetChangesAsync(Guid baselineId, CancellationToken cancellationToken = default) =>
        await _context.RequirementChanges.AsNoTracking().Where(x => x.BaselineId == baselineId)
            .OrderByDescending(x => x.ChangedAt).ToListAsync(cancellationToken);

    public Task AddAsync(RequirementBaseline baseline, CancellationToken cancellationToken = default) =>
        _context.RequirementBaselines.AddAsync(baseline, cancellationToken).AsTask();
}
