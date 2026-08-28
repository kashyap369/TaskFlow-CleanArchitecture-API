using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Domain.Interfaces.Planner;

public interface IPlannerResourceRepository
{
    Task<PlannerResource?> GetAsync(Guid resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlannerResource>> ListAsync(Guid boardId, CancellationToken cancellationToken = default);
    Task AddAsync(PlannerResource resource, CancellationToken cancellationToken = default);
    void Remove(PlannerResource resource);
}
