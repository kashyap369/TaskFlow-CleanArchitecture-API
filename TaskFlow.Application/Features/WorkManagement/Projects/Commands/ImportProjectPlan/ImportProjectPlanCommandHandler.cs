using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Entities.WorkManagement.SubTasks;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Domain.ValueObjects;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Application.Features.WorkManagement.Projects.Commands.ImportProjectPlan;

public sealed class ImportProjectPlanCommandHandler
    : IRequestHandler<ImportProjectPlanCommand, ImportProjectPlanResult>
{
    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly ISubTaskRepository _subTasks;
    private readonly IUserRepository _users;
    private readonly IOrganizationMemberRepository _members;
    private readonly ITeamRepository _teams;
    private readonly IOrganizationAccessGuard _accessGuard;
    private readonly IOrganizationPermissionChecker _permissionChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IPlannerBoardRepository _plannerBoards;
    private readonly IUnitOfWork _unitOfWork;

    public ImportProjectPlanCommandHandler(
        IProjectRepository projects,
        ITaskRepository tasks,
        ISubTaskRepository subTasks,
        IUserRepository users,
        IOrganizationMemberRepository members,
        ITeamRepository teams,
        IOrganizationAccessGuard accessGuard,
        IOrganizationPermissionChecker permissionChecker,
        ICurrentUserService currentUser,
        IPlannerBoardRepository plannerBoards,
        IUnitOfWork unitOfWork)
    {
        _projects = projects;
        _tasks = tasks;
        _subTasks = subTasks;
        _users = users;
        _members = members;
        _teams = teams;
        _accessGuard = accessGuard;
        _permissionChecker = permissionChecker;
        _currentUser = currentUser;
        _plannerBoards = plannerBoards;
        _unitOfWork = unitOfWork;
    }

    public async Task<ImportProjectPlanResult> Handle(
        ImportProjectPlanCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (await _users.GetByIdAsync(userId, cancellationToken) is null)
        {
            throw new NotFoundException("USER_NOT_FOUND", "User not found.");
        }

        var duplicateKey = request.Tasks
            .GroupBy(x => x.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1)?.Key;
        if (duplicateKey is not null)
        {
            throw new ConflictException("DUPLICATE_TASK_KEY", $"Task key '{duplicateKey}' is used more than once.");
        }

        var duplicateTitle = request.Tasks
            .GroupBy(x => x.Title.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1)?.Key;
        if (duplicateTitle is not null)
        {
            throw new ConflictException("DUPLICATE_TASK_TITLE", $"Task title '{duplicateTitle}' is used more than once.");
        }

        foreach (var task in request.Tasks)
        {
            var repeatedSubTask = task.SubTasks
                .GroupBy(x => x.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(x => x.Count() > 1)?.Key;
            if (repeatedSubTask is not null)
            {
                throw new ConflictException(
                    "DUPLICATE_SUBTASK_TITLE",
                    $"Subtask '{repeatedSubTask}' is repeated under task '{task.Title}'.");
            }

            if (task.StartDate < request.StartDate ||
                request.ExpectedCompletionDate.HasValue &&
                (task.ExpectedCompletionDate ?? task.StartDate) > request.ExpectedCompletionDate.Value)
            {
                throw new BusinessException(
                    "TASK_OUTSIDE_PROJECT_DATES",
                    $"Task '{task.Title}' must fit inside the project's date range.");
            }

        }

        Dictionary<string, int> teamIds = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> assigneeIds = new(StringComparer.OrdinalIgnoreCase);

        if (request.OrganizationId is int organizationId)
        {
            await _accessGuard.EnsureOrganizationAsync(organizationId, cancellationToken);
            await _permissionChecker.EnsurePermissionAsync(
                organizationId, userId, OrganizationPermissionNames.CreateProject, cancellationToken);

            if (await _projects.ExistsByNameAsync(organizationId, request.Title, cancellationToken))
            {
                throw new ConflictException("PROJECT_ALREADY_EXISTS", "Project already exists.");
            }

            var availableTeams = await _teams.GetByOrganizationIdAsync(organizationId, cancellationToken);
            teamIds = availableTeams
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);

            foreach (var teamName in request.Tasks.Select(x => x.TeamName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!teamIds.ContainsKey(teamName!.Trim()))
                {
                    throw new NotFoundException("TEAM_NOT_FOUND", $"Team '{teamName}' does not exist in this organization.");
                }
            }

            var emails = request.Tasks.Select(x => x.AssigneeEmail)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (emails.Length > 0)
            {
                await _permissionChecker.EnsurePermissionAsync(
                    organizationId, userId, OrganizationPermissionNames.AssignTask, cancellationToken);
            }

            foreach (var email in emails)
            {
                var normalized = email!.Trim();
                var assignee = await _users.GetByEmailAsync(new Email(normalized), cancellationToken);
                if (assignee is null || !await _members.IsActiveMemberAsync(organizationId, assignee.Id, cancellationToken))
                {
                    throw new NotFoundException(
                        "ASSIGNEE_NOT_FOUND",
                        $"'{normalized}' is not an active member of this organization.");
                }
                assigneeIds[normalized] = assignee.Id;
            }
        }
        else
        {
            if (await _projects.ExistsPersonalByNameAsync(userId, request.Title, cancellationToken))
            {
                throw new ConflictException("PROJECT_ALREADY_EXISTS", "A personal project with the same title already exists.");
            }

            if (request.Tasks.Any(x => !string.IsNullOrWhiteSpace(x.TeamName) || !string.IsNullOrWhiteSpace(x.AssigneeEmail)))
            {
                throw new BusinessException(
                    "PERSONAL_PLAN_HAS_ORGANIZATION_FIELDS",
                    "Personal project plans cannot contain teams or assignees.");
            }
        }

        var existingTasks = request.OrganizationId.HasValue
            ? await _tasks.GetByOrganizationIdAsync(request.OrganizationId.Value, cancellationToken)
            : (await _tasks.GetByCreatedByUserIdAsync(userId, cancellationToken)).Where(x => x.IsPersonal).ToArray();
        var existingTitles = existingTasks
            .Select(x => x.Title.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var collidingTask = request.Tasks.FirstOrDefault(x => existingTitles.Contains(x.Title.Trim()));
        if (collidingTask is not null)
        {
            throw new ConflictException(
                "TASK_ALREADY_EXISTS",
                $"A task named '{collidingTask.Title}' already exists in this workspace.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var project = new Project(
                request.Title.Trim(), request.Description?.Trim() ?? string.Empty,
                request.StartDate, request.OrganizationId, userId, request.ExpectedCompletionDate);
            await _projects.AddAsync(project, ct);
            if (!request.OrganizationId.HasValue)
            {
                await _plannerBoards.AddAsync(PlannerBoard.Create(project, userId), ct);
            }
            await _unitOfWork.SaveChangesAsync(ct);

            var createdTasks = new List<(TaskEntity Entity, ImportProjectPlanTask Source)>();
            foreach (var source in request.Tasks)
            {
                int? teamId = string.IsNullOrWhiteSpace(source.TeamName)
                    ? null
                    : teamIds[source.TeamName.Trim()];
                var task = new TaskEntity(
                    source.Title.Trim(), source.Description?.Trim() ?? string.Empty,
                    source.StartDate, source.Priority, request.OrganizationId, userId,
                    source.ExpectedCompletionDate, project.Id, teamId);
                task.SetEstimate(source.EstimateMinutes);
                await _tasks.AddAsync(task, ct);
                createdTasks.Add((task, source));
            }
            await _unitOfWork.SaveChangesAsync(ct);

            foreach (var (task, source) in createdTasks)
            {
                if (!string.IsNullOrWhiteSpace(source.AssigneeEmail))
                {
                    task.Assign(assigneeIds[source.AssigneeEmail.Trim()], userId);
                    _tasks.Update(task);
                }

                foreach (var title in source.SubTasks)
                {
                    var subTask = new SubTask(title.Trim(), task.Id);
                    await _subTasks.AddAsync(subTask, ct);
                    task.AddSubTask(subTask);
                }
            }
            await _unitOfWork.SaveChangesAsync(ct);

            return new ImportProjectPlanResult(
                project.Id,
                createdTasks.Count,
                createdTasks.Sum(x => x.Source.SubTasks.Count));
        }, cancellationToken);
    }
}
