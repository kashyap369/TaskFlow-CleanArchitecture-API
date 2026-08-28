using MediatR;
using TaskFlow.Application.Common.Authorization;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Queries.GetPlannerBoard;

public sealed record GetPlannerBoardQuery(int ProjectId)
    : IRequest<PlannerBoardDto>, IProjectScopedRequest;

public sealed class GetPlannerBoardQueryHandler
    : IRequestHandler<GetPlannerBoardQuery, PlannerBoardDto>
{
    private readonly IPlannerBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetPlannerBoardQueryHandler(
        IPlannerBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUserService)
    {
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _currentUserService = currentUserService;
    }

    public async Task<PlannerBoardDto> Handle(
        GetPlannerBoardQuery request,
        CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(
            request.ProjectId,
            _projectRepository,
            _currentUserService,
            cancellationToken);

        var board = await _boardRepository.GetSceneByProjectIdAsync(
            request.ProjectId,
            cancellationToken);

        if (board is null)
            throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");

        return new PlannerBoardDto(
            board.Id,
            board.ProjectId,
            board.CurrentRevision,
            board.SceneJson,
            board.UpdatedAt,
            board.LastOpenedAt);
    }
}
