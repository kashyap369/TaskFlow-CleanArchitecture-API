using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CompleteSubTask
{
    public sealed class CompleteSubTaskCommandHandler
        : IRequestHandler<CompleteSubTaskCommand>
    {
        private readonly ISubTaskRepository _subTaskRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteSubTaskCommandHandler(
            ISubTaskRepository subTaskRepository,
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _subTaskRepository = subTaskRepository;
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            CompleteSubTaskCommand request,
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

            subTask.Complete();

            task.RecalculateStatus();

            _subTaskRepository.Update(
                subTask);

            _taskRepository.Update(
                task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}