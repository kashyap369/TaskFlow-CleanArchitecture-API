using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.AssignTask
{
    public sealed class AssignTaskCommandHandler
        : IRequestHandler<AssignTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IOrganizationMemberRepository _organizationMemberRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public AssignTaskCommandHandler(
            ITaskRepository taskRepository,
            IOrganizationMemberRepository organizationMemberRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _organizationMemberRepository = organizationMemberRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            AssignTaskCommand request,
            CancellationToken cancellationToken)
        {
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

            if (!task.OrganizationId.HasValue)
            {
                throw new BusinessException(
                    "TASK_NOT_ASSIGNABLE",
                    "Personal tasks cannot be assigned.");
            }

            var organizationId = task.OrganizationId.Value;

            await _permissionChecker.EnsurePermissionAsync(
                organizationId,
                _currentUserService.UserId,
                OrganizationPermissionNames.AssignTask,
                cancellationToken);

            // The assignee must belong to the same organization.
            var isActiveMember =
                await _organizationMemberRepository
                    .IsActiveMemberAsync(
                        organizationId,
                        request.AssignedToUserId,
                        cancellationToken);

            if (!isActiveMember)
            {
                throw new ConflictException(
                    "NOT_AN_ORGANIZATION_MEMBER",
                    "Assignee is not an active member of the organization.");
            }

            task.Assign(
                request.AssignedToUserId,
                _currentUserService.UserId);

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
