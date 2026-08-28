using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Commands.SavePlannerScene;

public sealed class SavePlannerSceneCommandHandler
    : IRequestHandler<SavePlannerSceneCommand, SavePlannerSceneResult>
{
    private readonly IPlannerBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public SavePlannerSceneCommandHandler(
        IPlannerBoardRepository boardRepository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task<SavePlannerSceneResult> Handle(
        SavePlannerSceneCommand request,
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

        if (board.CurrentRevision != request.ExpectedRevision)
        {
            throw new ConflictException(
                "PLANNER_REVISION_CONFLICT",
                "This Planner board changed in another tab or device. Reload it before saving.");
        }

        var revision = board.SaveScene(
            request.SceneJson,
            request.ExpectedRevision,
            _currentUserService.UserId);

        await _boardRepository.AddRevisionAsync(revision, cancellationToken);
        await _boardRepository.PruneRevisionsAsync(
            board.Id,
            revision.RevisionNumber,
            TaskFlow.Domain.Entities.Planner.PlannerSceneDocument.RevisionRetentionLimit,
            cancellationToken);
        _boardRepository.Update(board);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SavePlannerSceneResult(
            board.Id,
            revision.RevisionNumber,
            revision.CreatedAt);
    }
}
