using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UnassignTask
{
    public sealed class UnassignTaskCommandHandler
        : IRequestHandler<UnassignTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IOrganizationPermissionChecker _permissionChecker;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public UnassignTaskCommandHandler(
            ITaskRepository taskRepository,
            IOrganizationPermissionChecker permissionChecker,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _permissionChecker = permissionChecker;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UnassignTaskCommand request,
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

            await _permissionChecker.EnsurePermissionAsync(
                task.OrganizationId.Value,
                _currentUserService.UserId,
                OrganizationPermissionNames.AssignTask,
                cancellationToken);

            task.Unassign(_currentUserService.UserId);

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
