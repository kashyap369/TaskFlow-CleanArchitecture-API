using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

using SubTaskEntity =
    TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.CreateSubTask
{
    public sealed class CreateSubTaskCommandHandler
        : IRequestHandler<CreateSubTaskCommand, int>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ISubTaskRepository _subTaskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateSubTaskCommandHandler(
            ITaskRepository taskRepository,
            ISubTaskRepository subTaskRepository,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _subTaskRepository = subTaskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            CreateSubTaskCommand request,
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

            var existingSubTask =
                await _subTaskRepository.GetByTitleAsync(
                    request.TaskId,
                    request.Title,
                    cancellationToken);

            if (existingSubTask is not null)
            {
                throw new ConflictException(
                    "SUBTASK_ALREADY_EXISTS",
                    "SubTask with same title already exists.");
            }

            var subTask = new SubTaskEntity(
                request.Title,
                request.TaskId);

            await _subTaskRepository.AddAsync(
                subTask,
                cancellationToken);

            task.AddSubTask(subTask);

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return subTask.Id;
        }
    }
}