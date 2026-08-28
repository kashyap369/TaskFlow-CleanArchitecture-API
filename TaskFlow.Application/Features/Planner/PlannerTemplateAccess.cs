using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Planner;

namespace TaskFlow.Application.Features.Planner;

internal static class PlannerTemplateAccess
{
    public static async Task<PlannerTemplateVersion?> ResolveAsync(Guid? versionId, PlannerNodeType expectedType,
        IPlannerTemplateRepository templates, CancellationToken cancellationToken)
    {
        if (versionId is null) return null;
        var version = await templates.GetPublishedVersionAsync(versionId.Value, cancellationToken)
            ?? throw new NotFoundException("PLANNER_TEMPLATE_VERSION_NOT_AVAILABLE", "The selected Planner template is not published and active.");
        if (version.ObjectType != expectedType) throw new ConflictException("PLANNER_TEMPLATE_TYPE_MISMATCH", "The selected template does not match this work object type.");
        return version;
    }
}
