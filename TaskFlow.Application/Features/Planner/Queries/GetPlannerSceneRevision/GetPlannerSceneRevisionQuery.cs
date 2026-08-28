using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerSceneRevision;

public sealed record GetPlannerSceneRevisionQuery(int ProjectId, int Revision)
    : IRequest<PlannerSceneRevisionDto>, IProjectScopedRequest;

public sealed class GetPlannerSceneRevisionQueryHandler
    : IRequestHandler<GetPlannerSceneRevisionQuery, PlannerSceneRevisionDto>
{
    private readonly IPlannerBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetPlannerSceneRevisionQueryHandler(
        IPlannerBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUserService)
    {
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _currentUserService = currentUserService;
    }

    public async Task<PlannerSceneRevisionDto> Handle(
        GetPlannerSceneRevisionQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(
            request.ProjectId,
            _projectRepository,
            _currentUserService,
            cancellationToken);

        var board = await _boardRepository.GetSceneByProjectIdAsync(request.ProjectId, cancellationToken);
        if (board is null)
            throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");

        var revision = await _boardRepository.GetRevisionAsync(
            board.Id,
            request.Revision,
            cancellationToken);

        if (revision is null)
            throw new NotFoundException("PLANNER_REVISION_NOT_FOUND", "Planner scene revision not found.");

        return new PlannerSceneRevisionDto(
            board.Id,
            board.ProjectId,
            revision.RevisionNumber,
            revision.SceneJson,
            revision.CreatedAt,
            revision.CreatedByUserId);
    }
}
