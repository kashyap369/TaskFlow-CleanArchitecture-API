using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

using TaskEntity =
    TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CreateTask
{
    public sealed class CreateTaskCommandHandler
        : IRequestHandler<CreateTaskCommand, int>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly ITeamRepository _teamRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public CreateTaskCommandHandler(
            ITaskRepository taskRepository,
            IProjectRepository projectRepository,
            IOrganizationRepository organizationRepository,
            ITeamRepository teamRepository,
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _teamRepository = teamRepository;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _accessGuard = accessGuard;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateTaskCommand request,
            CancellationToken cancellationToken)
        {
            // An organization task must name a real organization the
            // caller belongs to. A personal task (null OrganizationId)
            // skips this entirely — it is scoped to its creator.
            if (request.OrganizationId.HasValue)
            {
                var organization =
                    await _organizationRepository.GetByIdAsync(
                        request.OrganizationId.Value,
                        cancellationToken);

                if (organization is null)
                {
                    throw new NotFoundException(
                        "ORGANIZATION_NOT_FOUND",
                        "Organization not found.");
                }

                // Owner or active member only — without this, any
                // authenticated user could plant a task in any org.
                await _accessGuard.EnsureOrganizationAsync(
                    request.OrganizationId.Value,
                    cancellationToken);
            }
            else if (request.ProjectId.HasValue)
            {
                // Defence in depth: the validator rejects this too, but
                // the domain would otherwise throw ArgumentException.
                throw new ConflictException(
                    "PROJECT_REQUIRES_ORGANIZATION",
                    "A personal task cannot belong to a project.");
            }
            else if (request.TeamId.HasValue)
            {
                // Same rule for teams — they are an organization concept.
                throw new ConflictException(
                    "TEAM_REQUIRES_ORGANIZATION",
                    "A personal task cannot belong to a team.");
            }

            // The creator is always the logged-in user (taken
            // from the JWT), never from the request body.
            var createdByUserId =
                _currentUserService.UserId;

            var user =
                await _userRepository.GetByIdAsync(
                    createdByUserId,
                    cancellationToken);

            if (user is null)
            {
                throw new NotFoundException(
                    "USER_NOT_FOUND",
                    "User not found.");
            }

            if (request.ProjectId.HasValue)
            {
                var project =
                    await _projectRepository.GetByIdAsync(
                        request.ProjectId.Value,
                        cancellationToken);

                if (project is null)
                {
                    throw new NotFoundException(
                        "PROJECT_NOT_FOUND",
                        "Project not found.");
                }

                if (project.OrganizationId != request.OrganizationId)
                {
                    throw new ConflictException(
                        "PROJECT_ORGANIZATION_MISMATCH",
                        "Project does not belong to the organization.");
                }
            }

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

                // Same guard as the project check above: a team id
                // from another organization must not be accepted.
                if (team.OrganizationId != request.OrganizationId)
                {
                    throw new ConflictException(
                        "TEAM_ORGANIZATION_MISMATCH",
                        "Team does not belong to the organization.");
                }
            }

            var existingTask =
                await _taskRepository.GetByTitleAsync(
                    request.OrganizationId,
                    createdByUserId,
                    request.Title,
                    cancellationToken);

            if (existingTask is not null)
            {
                throw new ConflictException(
                    "TASK_ALREADY_EXISTS",
                    "Task with same title already exists.");
            }

            var task = new TaskEntity(
                request.Title,
                request.Description,
                request.StartDate,
                request.Priority,
                request.OrganizationId,
                createdByUserId,
                request.ExpectedCompletionDate,
                request.ProjectId,
                request.TeamId);

            await _taskRepository.AddAsync(
                task,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return task.Id;
        }
    }
}