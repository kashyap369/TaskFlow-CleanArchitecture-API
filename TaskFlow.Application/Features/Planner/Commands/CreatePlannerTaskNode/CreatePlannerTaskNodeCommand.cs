using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;
using TaskFlow.Application.Contracts.Planner;

namespace TaskFlow.Application.Features.Planner.Commands.CreatePlannerTaskNode;

public sealed record CreatePlannerTaskNodeCommand(int ProjectId, string ElementId, string Title,
    string Description, DateTime StartDate, TaskPriority Priority, DateTime? ExpectedCompletionDate,
    Guid? TemplateVersionId = null, string? ChangeReason = null) : IRequest<Guid>;

public sealed class CreatePlannerTaskNodeCommandHandler : IRequestHandler<CreatePlannerTaskNodeCommand, Guid>
{
    private readonly IProjectRepository _projects; private readonly ITaskRepository _tasks;
    private readonly IPlannerBoardRepository _boards; private readonly IUnitOfWork _unitOfWork;
    private readonly IPlannerTemplateRepository _templates;
    private readonly IRequirementChangeContext _changeContext;
    private readonly TaskFlow.Application.Contracts.Security.ICurrentUserService _currentUser;
    public CreatePlannerTaskNodeCommandHandler(IProjectRepository projects, ITaskRepository tasks,
        IPlannerBoardRepository boards, IUnitOfWork unitOfWork,
        TaskFlow.Application.Contracts.Security.ICurrentUserService currentUser, IPlannerTemplateRepository templates,
        IRequirementChangeContext changeContext)
    { _projects = projects; _tasks = tasks; _boards = boards; _unitOfWork = unitOfWork; _currentUser = currentUser; _templates = templates; _changeContext = changeContext; }

    public async Task<Guid> Handle(CreatePlannerTaskNodeCommand request, CancellationToken cancellationToken)
    {
        var project = await PersonalPlannerAccess.GetOwnedProjectAsync(request.ProjectId, _projects, _currentUser, cancellationToken);
        _changeContext.SetReason(request.ChangeReason);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");
        if (board.Nodes.Any(x => x.ElementId == request.ElementId))
            throw new ConflictException("PLANNER_ELEMENT_ALREADY_LINKED", "This canvas element is already linked.");
        if (await _tasks.GetByTitleAsync(null, _currentUser.UserId, request.Title, cancellationToken) is not null)
            throw new ConflictException("TASK_ALREADY_EXISTS", "Task with same title already exists.");

        var task = new TaskEntity(request.Title, request.Description, request.StartDate, request.Priority,
            null, _currentUser.UserId, request.ExpectedCompletionDate, project.Id);
        await _tasks.AddAsync(task, cancellationToken);
        var node = board.LinkTask(request.ElementId, task);
        var version = await PlannerTemplateAccess.ResolveAsync(request.TemplateVersionId, Domain.Enums.Planner.PlannerNodeType.Task, _templates, cancellationToken);
        if (version is not null) node.ApplyTemplate(version);
        _boards.Update(board);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return node.Id;
    }
}
