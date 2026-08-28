using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using SubTaskEntity = TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask;
using TaskFlow.Application.Contracts.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.CreatePlannerSubTaskNode;

public sealed record CreatePlannerSubTaskNodeCommand(int ProjectId, string ElementId, int TaskId, string Title,
    Guid? TemplateVersionId = null, string? ChangeReason = null) : IRequest<Guid>;

public sealed class CreatePlannerSubTaskNodeCommandHandler : IRequestHandler<CreatePlannerSubTaskNodeCommand, Guid>
{
    private readonly IProjectRepository _projects; private readonly ITaskRepository _tasks; private readonly ISubTaskRepository _subTasks;
    private readonly IPlannerBoardRepository _boards; private readonly IUnitOfWork _unitOfWork;
    private readonly IPlannerTemplateRepository _templates;
    private readonly IRequirementChangeContext _changeContext;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;
    public CreatePlannerSubTaskNodeCommandHandler(IProjectRepository projects, ITaskRepository tasks,
        ISubTaskRepository subTasks, IPlannerBoardRepository boards, IUnitOfWork unitOfWork,
        TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser, IPlannerTemplateRepository templates,
        IRequirementChangeContext changeContext)
    { _projects = projects; _tasks = tasks; _subTasks = subTasks; _boards = boards; _unitOfWork = unitOfWork; _currentUser = currentUser; _templates = templates; _changeContext = changeContext; }

    public async Task<Guid> Handle(CreatePlannerSubTaskNodeCommand request, CancellationToken cancellationToken)
    {
        await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        _changeContext.SetReason(request.ChangeReason);
        var task = await _tasks.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("TASK_NOT_FOUND", "Task not found.");
        if (task.ProjectId != request.ProjectId || !task.IsPersonal || task.CreatedByUserId != _currentUser.UserId)
            throw new ForbiddenException("ACCESS_DENIED", "Task does not belong to this Planner project.");
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        if (board.Nodes.Any(x => x.ElementId == request.ElementId))
            throw new ConflictException("PLANNER_ELEMENT_ALREADY_LINKED", "This canvas element is already linked.");
        if (await _subTasks.GetByTitleAsync(request.TaskId, request.Title, cancellationToken) is not null)
            throw new ConflictException("SUBTASK_ALREADY_EXISTS", "Subtask with same title already exists.");

        var subTask = new SubTaskEntity(request.Title, request.TaskId);
        await _subTasks.AddAsync(subTask, cancellationToken);
        task.AddSubTask(subTask);
        _tasks.Update(task);
        var node = board.LinkSubTask(request.ElementId, subTask, task);
        var version = await PlannerTemplateAccess.ResolveAsync(request.TemplateVersionId, Domain.Enums.Planner.PlannerNodeType.SubTask, _templates, cancellationToken);
        if (version is not null) node.ApplyTemplate(version);
        _boards.Update(board);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return node.Id;
    }
}
