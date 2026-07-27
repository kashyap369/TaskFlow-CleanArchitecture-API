using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.DeleteSubTask
{
    public sealed class DeleteSubTaskCommandHandler
        : IRequestHandler<DeleteSubTaskCommand>
    {
        private readonly ISubTaskRepository _subTaskRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteSubTaskCommandHandler(
            ISubTaskRepository subTaskRepository,
            IOrganizationAccessGuard accessGuard,
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _subTaskRepository = subTaskRepository;
            _accessGuard = accessGuard;
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteSubTaskCommand request,
            CancellationToken cancellationToken)
        {
            var subTask =
                await _subTaskRepository.GetByIdAsync(
                    request.SubTaskId,
                    cancellationToken);

            if (subTask is null)
            {
                throw new NotFoundException(
                    "SUBTASK_NOT_FOUND",
                    "SubTask not found.");
            }

            // The parent task decides who may touch this subtask:
            // org task -> owner/active member; personal task -> its creator.
            await _accessGuard.EnsureTaskAsync(
                subTask.TaskId,
                cancellationToken);

            var task =
                await _taskRepository.GetByIdAsync(
                    subTask.TaskId,
                    cancellationToken);

            if (task is null)
            {
                throw new NotFoundException(
                    "TASK_NOT_FOUND",
                    "Task not found.");
            }

            task.RemoveSubTask(
                subTask.Id);

            _subTaskRepository.Remove(
                subTask);

            _taskRepository.Update(
                task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}