using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Planner;

public sealed class PlannerTemplateRepository(TaskFlowDbContext context) : IPlannerTemplateRepository
{
    public Task<PlannerTemplate?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PlannerTemplates.Include(x => x.Versions).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PlannerTemplateVersion?> GetPublishedVersionAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.PlannerTemplateVersions.Include(x => x.Template).FirstOrDefaultAsync(x => x.Id == id &&
            x.Template.Status == PlannerTemplateStatus.Published && x.Template.IsActive &&
            x.Template.CurrentVersionNumber == x.VersionNumber, cancellationToken);

    public Task AddAsync(PlannerTemplate template, CancellationToken cancellationToken = default) =>
        context.PlannerTemplates.AddAsync(template, cancellationToken).AsTask();
    public void AddVersion(PlannerTemplateVersion version) => context.Entry(version).State = EntityState.Added;
}
