using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.DTOs;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Planner;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.Planner.Commands.FinalizePrimaryRequirements;

public sealed record FinalizePrimaryRequirementsCommand(int ProjectId) : IRequest<RequirementBaselineDto>;

public sealed class FinalizePrimaryRequirementsCommandHandler
    : IRequestHandler<FinalizePrimaryRequirementsCommand, RequirementBaselineDto>
{
    private readonly IProjectRepository _projects;
    private readonly IPlannerBoardRepository _boards;
    private readonly IRequirementBaselineRepository _baselines;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public FinalizePrimaryRequirementsCommandHandler(
        IProjectRepository projects,
        IPlannerBoardRepository boards,
        IRequirementBaselineRepository baselines,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _boards = boards;
        _baselines = baselines;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<RequirementBaselineDto> Handle(
        FinalizePrimaryRequirementsCommand request,
        CancellationToken cancellationToken)
    {
        var project = await PersonalPlannerAccess.GetOwnedProjectAsync(
            request.ProjectId, _projects, _currentUser, cancellationToken);
        var board = await _boards.GetByProjectIdAsync(request.ProjectId, cancellationToken)
            ?? throw new NotFoundException("PLANNER_BOARD_NOT_FOUND", "Planner board not found.");

        if (await _baselines.GetLatestAsync(request.ProjectId, cancellationToken) is not null)
        {
            throw new ConflictException(
                "PRIMARY_REQUIREMENTS_ALREADY_FINALIZED",
                "Primary requirements have already been finalized for this project.");
        }

        var baseline = RequirementBaseline.Create(board, 1, _currentUser.UserId);
        var order = BuildOrderMap(board);
        baseline.AddSnapshot(new RequirementSnapshot(
            baseline.Id,
            RequirementEntityType.Project,
            project.Id,
            null,
            order.GetValueOrDefault((RequirementEntityType.Project, project.Id), 0),
            project.Title,
            RequirementFields.ForProject(project)));

        var fallbackOrder = order.Count + 1;
        foreach (var task in project.Tasks.OrderBy(x => order.GetValueOrDefault(
                     (RequirementEntityType.Task, x.Id), fallbackOrder)).ThenBy(x => x.Id))
        {
            baseline.AddSnapshot(new RequirementSnapshot(
                baseline.Id,
                RequirementEntityType.Task,
                task.Id,
                project.Id,
                order.GetValueOrDefault((RequirementEntityType.Task, task.Id), fallbackOrder++),
                task.Title,
                RequirementFields.ForTask(task)));

            foreach (var subTask in task.SubTasks.OrderBy(x => order.GetValueOrDefault(
                         (RequirementEntityType.SubTask, x.Id), fallbackOrder)).ThenBy(x => x.Id))
            {
                baseline.AddSnapshot(new RequirementSnapshot(
                    baseline.Id,
                    RequirementEntityType.SubTask,
                    subTask.Id,
                    task.Id,
                    order.GetValueOrDefault((RequirementEntityType.SubTask, subTask.Id), fallbackOrder++),
                    subTask.Title,
                    RequirementFields.ForSubTask(subTask)));
            }
        }

        await _baselines.AddAsync(baseline, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return RequirementDtoMapper.ToDto(baseline);
    }

    private static Dictionary<(RequirementEntityType Type, int Id), int> BuildOrderMap(PlannerBoard board)
    {
        var result = new Dictionary<(RequirementEntityType, int), int>();
        var index = 0;
        foreach (var node in board.Nodes.OrderBy(x => x.CreatedAt))
        {
            if (node.ProjectId is int projectId) result[(RequirementEntityType.Project, projectId)] = index++;
            else if (node.TaskId is int taskId) result[(RequirementEntityType.Task, taskId)] = index++;
            else if (node.SubTaskId is int subTaskId) result[(RequirementEntityType.SubTask, subTaskId)] = index++;
        }
        return result;
    }
}
