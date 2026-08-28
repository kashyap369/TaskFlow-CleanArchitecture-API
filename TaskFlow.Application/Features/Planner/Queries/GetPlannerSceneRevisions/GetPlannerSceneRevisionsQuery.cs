using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerSceneRevisions;

public sealed record GetPlannerSceneRevisionsQuery(int ProjectId)
    : IRequest<IReadOnlyList<PlannerSceneRevisionListItemDto>>, IProjectScopedRequest;

public sealed class GetPlannerSceneRevisionsQueryHandler
    : IRequestHandler<GetPlannerSceneRevisionsQuery, IReadOnlyList<PlannerSceneRevisionListItemDto>>
{
    private readonly IPlannerBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetPlannerSceneRevisionsQueryHandler(
        IPlannerBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUserService)
    {
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<PlannerSceneRevisionListItemDto>> Handle(
        GetPlannerSceneRevisionsQuery request,
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

        var revisions = await _boardRepository.GetRevisionsAsync(board.Id, cancellationToken);
        return revisions
            .Select(x => new PlannerSceneRevisionListItemDto(
                x.RevisionNumber,
                x.CreatedAt,
                x.CreatedByUserId))
            .ToList();
    }
}
