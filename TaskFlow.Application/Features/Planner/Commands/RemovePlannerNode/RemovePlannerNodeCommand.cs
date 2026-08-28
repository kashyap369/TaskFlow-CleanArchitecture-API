using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Application.Contracts.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.RemovePlannerNode;

public sealed record RemovePlannerNodeCommand(int ProjectId, Guid NodeId, bool DeleteEntity, string? ChangeReason = null) : IRequest;

public sealed class RemovePlannerNodeCommandHandler : IRequestHandler<RemovePlannerNodeCommand>
{
    private readonly IProjectRepository _projects; private readonly ITaskRepository _tasks;
    private readonly ISubTaskRepository _subTasks; private readonly IPlannerBoardRepository _boards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRequirementChangeContext _changeContext;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;
    public RemovePlannerNodeCommandHandler(IProjectRepository projects, ITaskRepository tasks,
        ISubTaskRepository subTasks, IPlannerBoardRepository boards, IUnitOfWork unitOfWork,
        TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser,
        IRequirementChangeContext changeContext)
    { _projects = projects; _tasks = tasks; _subTasks = subTasks; _boards = boards; _unitOfWork = unitOfWork; _currentUser = currentUser; _changeContext = changeContext; }

    public async Task Handle(RemovePlannerNodeCommand request, CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        _changeContext.SetReason(request.ChangeReason);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        var node = board.FindNode(request.NodeId)
            ?? throw new NotFoundException("PLANNER_NODE_NOT_FOUND", "Planner node not found.");

        if (request.DeleteEntity && node.NodeType == PlannerNodeType.Project)
            throw new ConflictException("PLANNER_PROJECT_DELETE_REQUIRES_PROJECT_FLOW",
                "The project card can be unlinked here, but deleting the project requires the Projects screen.");

        if (request.DeleteEntity && node.NodeType == PlannerNodeType.Task && node.TaskId is int taskId)
        {
            var task = await _tasks.GetByIdAsync(taskId, cancellationToken)
                ?? throw new NotFoundException("TASK_NOT_FOUND", "Task not found.");
            if (task.ProjectId != request.ProjectId || task.CreatedByUserId != _currentUser.UserId)
                throw new ForbiddenException("ACCESS_DENIED", "Task does not belong to this Planner project.");
            var subTaskIds = task.SubTasks.Select(x => x.Id).ToHashSet();
            foreach (var subTask in task.SubTasks.ToList())
                _subTasks.Remove(subTask);
            foreach (var childNode in board.Nodes.Where(x => x.SubTaskId.HasValue && subTaskIds.Contains(x.SubTaskId.Value)).ToList())
                board.UnlinkNode(childNode.Id);
            _tasks.Remove(task);
        }
        else if (request.DeleteEntity && node.NodeType == PlannerNodeType.SubTask && node.SubTaskId is int subTaskId)
        {
            var subTask = await _subTasks.GetByIdAsync(subTaskId, cancellationToken)
                ?? throw new NotFoundException("SUBTASK_NOT_FOUND", "Subtask not found.");
            var task = await _tasks.GetByIdAsync(subTask.TaskId, cancellationToken)
                ?? throw new NotFoundException("TASK_NOT_FOUND", "Parent task not found.");
            if (task.ProjectId != request.ProjectId || task.CreatedByUserId != _currentUser.UserId)
                throw new ForbiddenException("ACCESS_DENIED", "Subtask does not belong to this Planner project.");
            _subTasks.Remove(subTask);
            task.RemoveSubTask(subTask.Id);
            _tasks.Update(task);
        }

        board.UnlinkNode(node.Id);
        _boards.Update(board);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
