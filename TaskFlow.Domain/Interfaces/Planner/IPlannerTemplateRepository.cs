using TaskFlow.Domain.Entities.Planner;

namespace TaskFlow.Domain.Interfaces.Planner;

public interface IPlannerTemplateRepository
{
    Task<PlannerTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlannerTemplateVersion?> GetPublishedVersionAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PlannerTemplate template, CancellationToken cancellationToken = default);
    void AddVersion(PlannerTemplateVersion version);
}
