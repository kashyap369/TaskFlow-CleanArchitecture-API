using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetRequirementChanges;

public sealed record GetRequirementChangesQuery(int ProjectId, RequirementChangeType? ChangeType)
    : IRequest<IReadOnlyList<RequirementChangeDto>>;

public sealed class GetRequirementChangesQueryHandler
    : IRequestHandler<GetRequirementChangesQuery, IReadOnlyList<RequirementChangeDto>>
{
    private readonly IProjectRepository _projects; private readonly IRequirementBaselineRepository _baselines;
    private readonly ICurrentUserService _currentUser;
    public GetRequirementChangesQueryHandler(IProjectRepository projects,
        IRequirementBaselineRepository baselines, ICurrentUserService currentUser)
    { _projects = projects; _baselines = baselines; _currentUser = currentUser; }

    public async Task<IReadOnlyList<RequirementChangeDto>> Handle(GetRequirementChangesQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var baseline = await _baselines.GetLatestAsync(request.ProjectId, cancellationToken);
        if (baseline is null) return Array.Empty<RequirementChangeDto>();
        var changes = await _baselines.GetChangesAsync(baseline.Id, cancellationToken);
        return changes.Where(x => !request.ChangeType.HasValue || x.ChangeType == request.ChangeType)
            .Select(RequirementDtoMapper.ToDto).ToList();
    }
}
