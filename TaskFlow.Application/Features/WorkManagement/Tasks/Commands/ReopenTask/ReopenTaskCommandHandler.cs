using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ReopenTask
{
    public sealed class ReopenTaskCommandHandler
        : IRequestHandler<ReopenTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public ReopenTaskCommandHandler(
            ITaskRepository taskRepository,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _accessGuard = accessGuard;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            ReopenTaskCommand request,
            CancellationToken cancellationToken)
        {
            // Org task -> owner/active member; personal task -> its creator.
            // Throws NotFound when the task does not exist.
            await _accessGuard.EnsureTaskAsync(
                request.TaskId,
                cancellationToken);

            // GetByIdAsync already Includes SubTasks, which Task.Reopen needs:
            // when a task has subtasks, they decide the resulting status.
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

            task.Reopen();

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
