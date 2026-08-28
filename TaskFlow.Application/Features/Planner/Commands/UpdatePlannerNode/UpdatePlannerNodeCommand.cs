using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Application.Contracts.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.UpdatePlannerNode;

public sealed record UpdatePlannerNodeCommand(
    int ProjectId, Guid NodeId, string Title, string? Description, DateTime? ExpectedCompletionDate,
    TaskPriority? Priority, string? ProblemStatement, decimal? BudgetAmount, string? BudgetCurrency,
    int? ApproximateDurationWeeks, string? ChangeReason = null) : IRequest;

public sealed class UpdatePlannerNodeCommandHandler : IRequestHandler<UpdatePlannerNodeCommand>
{
    private readonly IProjectRepository _projects; private readonly ITaskRepository _tasks;
    private readonly ISubTaskRepository _subTasks; private readonly IPlannerBoardRepository _boards;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRequirementChangeContext _changeContext;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;
    public UpdatePlannerNodeCommandHandler(IProjectRepository projects, ITaskRepository tasks,
        ISubTaskRepository subTasks, IPlannerBoardRepository boards, IUnitOfWork unitOfWork,
        TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser,
        IRequirementChangeContext changeContext)
    { _projects = projects; _tasks = tasks; _subTasks = subTasks; _boards = boards; _unitOfWork = unitOfWork; _currentUser = currentUser; _changeContext = changeContext; }

    public async Task Handle(UpdatePlannerNodeCommand request, CancellationToken cancellationToken)
    {
        var project = await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        _changeContext.SetReason(request.ChangeReason);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        var node = board.FindNode(request.NodeId)
            ?? throw new NotFoundException("PLANNER_NODE_NOT_FOUND", "Planner node not found.");

        if (node.NodeType == PlannerNodeType.Project)
        {
            project.UpdateDetails(request.Title, request.Description ?? string.Empty, request.ExpectedCompletionDate,
                request.ProblemStatement, request.BudgetAmount, request.BudgetCurrency, request.ApproximateDurationWeeks);
            _projects.Update(project);
        }
        else if (node.NodeType == PlannerNodeType.Task && node.TaskId is int taskId)
        {
            var task = await _tasks.GetByIdAsync(taskId, cancellationToken)
                ?? throw new NotFoundException("TASK_NOT_FOUND", "Task not found.");
            if (task.ProjectId != request.ProjectId || task.CreatedByUserId != _currentUser.UserId)
                throw new ForbiddenException("ACCESS_DENIED", "Task does not belong to this Planner project.");
            task.UpdateDetails(request.Title, request.Description ?? string.Empty,
                request.Priority ?? task.Priority, request.ExpectedCompletionDate);
            _tasks.Update(task);
        }
        else if (node.NodeType == PlannerNodeType.SubTask && node.SubTaskId is int subTaskId)
        {
            var subTask = await _subTasks.GetByIdAsync(subTaskId, cancellationToken)
                ?? throw new NotFoundException("SUBTASK_NOT_FOUND", "Subtask not found.");
            var parent = await _tasks.GetByIdAsync(subTask.TaskId, cancellationToken)
                ?? throw new NotFoundException("TASK_NOT_FOUND", "Parent task not found.");
            if (parent.ProjectId != request.ProjectId || parent.CreatedByUserId != _currentUser.UserId)
                throw new ForbiddenException("ACCESS_DENIED", "Subtask does not belong to this Planner project.");
            subTask.UpdateTitle(request.Title);
            _subTasks.Update(subTask);
        }
        else
        {
            throw new ConflictException("PLANNER_NODE_TYPE_UNSUPPORTED", "This Planner node cannot be edited yet.");
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
