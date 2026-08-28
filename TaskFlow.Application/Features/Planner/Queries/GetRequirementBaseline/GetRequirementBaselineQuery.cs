using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetRequirementBaseline;

public sealed record GetRequirementBaselineQuery(int ProjectId, Guid BaselineId) : IRequest<RequirementBaselineDto>;

public sealed class GetRequirementBaselineQueryHandler : IRequestHandler<GetRequirementBaselineQuery, RequirementBaselineDto>
{
    private readonly IProjectRepository _projects; private readonly IRequirementBaselineRepository _baselines;
    private readonly ICurrentUserService _currentUser;
    public GetRequirementBaselineQueryHandler(IProjectRepository projects,
        IRequirementBaselineRepository baselines, ICurrentUserService currentUser)
    { _projects = projects; _baselines = baselines; _currentUser = currentUser; }

    public async Task<RequirementBaselineDto> Handle(GetRequirementBaselineQuery request, CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        var baseline = await _baselines.GetByIdAsync(request.ProjectId, request.BaselineId, cancellationToken)
            ?? throw new NotFoundException("REQUIREMENT_BASELINE_NOT_FOUND", "Requirement baseline not found.");
        return RequirementDtoMapper.ToDto(baseline);
    }
}
