using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTaskToTeam
{
    public sealed class AssignTaskToTeamCommandHandler
        : IRequestHandler<AssignTaskToTeamCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public AssignTaskToTeamCommandHandler(
            ITaskRepository taskRepository,
            ITeamRepository teamRepository,
            IOrganizationAccessGuard accessGuard,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _teamRepository = teamRepository;
            _accessGuard = accessGuard;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            AssignTaskToTeamCommand request,
            CancellationToken cancellationToken)
        {
            // Org task -> owner/active member; personal task -> creator.
            // Throws NotFound when the task does not exist.
            await _accessGuard.EnsureTaskAsync(
                request.TaskId,
                cancellationToken);

            var task =
                await _taskRepository.GetByIdAsync(
                    request.TaskId,
                    cancellationToken);

            if (task is null)
            {
                throw new NotFoundException(
                    "TASK_NOT_FOUND",
                    "Task not found.");
            }

            if (task.IsPersonal)
            {
                throw new ConflictException(
                    "TEAM_REQUIRES_ORGANIZATION",
                    "A personal task cannot belong to a team.");
            }

            await _permissionChecker.EnsurePermissionAsync(
                task.OrganizationId!.Value,
                _currentUserService.UserId,
                OrganizationPermissionNames.ManageTasks,
                cancellationToken);

            if (request.TeamId.HasValue)
            {
                var team =
                    await _teamRepository.GetByIdAsync(
                        request.TeamId.Value,
                        cancellationToken);

                if (team is null)
                {
                    throw new NotFoundException(
                        "TEAM_NOT_FOUND",
                        "Team not found.");
                }

                // The team must live in the same organization as the
                // task — otherwise a task would be visible under a
                // team belonging to a different workspace.
                if (team.OrganizationId != task.OrganizationId)
                {
                    throw new ConflictException(
                        "TEAM_ORGANIZATION_MISMATCH",
                        "Team does not belong to the organization.");
                }
            }

            task.AssignToTeam(request.TeamId);

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
