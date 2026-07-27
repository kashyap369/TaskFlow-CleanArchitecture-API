using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask
{
    public sealed class CompleteTaskCommandHandler
        : IRequestHandler<CompleteTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteTaskCommandHandler(
            ITaskRepository taskRepository,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _accessGuard = accessGuard;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            CompleteTaskCommand request,
            CancellationToken cancellationToken)
        {
            // Org task -> owner/active member; personal task -> its creator.
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

            task.Complete();

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}