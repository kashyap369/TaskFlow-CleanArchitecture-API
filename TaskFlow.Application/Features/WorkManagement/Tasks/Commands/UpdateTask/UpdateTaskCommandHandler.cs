using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.UpdateTask
{
    public sealed class UpdateTaskCommandHandler
        : IRequestHandler<UpdateTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateTaskCommandHandler(
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateTaskCommand request,
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

            var existingTask =
                await _taskRepository.GetByTitleAsync(
                    task.OrganizationId,
                    request.Title,
                    cancellationToken);

            if (existingTask is not null &&
                existingTask.Id != task.Id)
            {
                throw new ConflictException(
                    "TASK_ALREADY_EXISTS",
                    "Task with same title already exists.");
            }

            task.UpdateDetails(
                request.Title,
                request.Description,
                request.Priority,
                request.ExpectedCompletionDate);

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}