using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetRequirementBaselines;

public sealed record GetRequirementBaselinesQuery(int ProjectId) : IRequest<IReadOnlyList<RequirementBaselineListItemDto>>;

public sealed class GetRequirementBaselinesQueryHandler
    : IRequestHandler<GetRequirementBaselinesQuery, IReadOnlyList<RequirementBaselineListItemDto>>
{
    private readonly IProjectRepository _projects;
    private readonly IRequirementBaselineRepository _baselines;
    private readonly ICurrentUserService _currentUser;

    public GetRequirementBaselinesQueryHandler(IProjectRepository projects,
        IRequirementBaselineRepository baselines, ICurrentUserService currentUser)
    { _projects = projects; _baselines = baselines; _currentUser = currentUser; }

    public async Task<IReadOnlyList<RequirementBaselineListItemDto>> Handle(
        GetRequirementBaselinesQuery request, CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var items = await _baselines.GetAllAsync(request.ProjectId, cancellationToken);
        return items.Select(x => new RequirementBaselineListItemDto(
            x.Id, x.BaselineNumber, x.Snapshots.Count, x.FinalizedByUserId, x.FinalizedAt)).ToList();
    }
}
