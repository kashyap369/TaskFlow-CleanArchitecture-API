using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Domain.Interfaces.Planner;

public interface IRequirementBaselineRepository
{
    Task<RequirementBaseline?> GetLatestAsync(int projectId, CancellationToken cancellationToken = default);
    Task<RequirementBaseline?> GetByIdAsync(int projectId, Guid baselineId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementBaseline>> GetAllAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementChange>> GetChangesAsync(Guid baselineId, CancellationToken cancellationToken = default);
    Task AddAsync(RequirementBaseline baseline, CancellationToken cancellationToken = default);
}
