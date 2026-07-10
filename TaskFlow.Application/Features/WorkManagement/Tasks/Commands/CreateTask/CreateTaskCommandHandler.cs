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
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public CreateTaskCommandHandler(
            ITaskRepository taskRepository,
            IProjectRepository projectRepository,
            IOrganizationRepository organizationRepository,
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateTaskCommand request,
            CancellationToken cancellationToken)
        {
            var organization =
                await _organizationRepository.GetByIdAsync(
                    request.OrganizationId,
                    cancellationToken);

            if (organization is null)
            {
                throw new NotFoundException(
                    "ORGANIZATION_NOT_FOUND",
                    "Organization not found.");
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

            var existingTask =
                await _taskRepository.GetByTitleAsync(
                    request.OrganizationId,
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
                request.ProjectId);

            await _taskRepository.AddAsync(
                task,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return task.Id;
        }
    }
}