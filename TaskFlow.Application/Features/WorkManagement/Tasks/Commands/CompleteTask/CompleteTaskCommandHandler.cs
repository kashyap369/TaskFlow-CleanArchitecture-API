using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.CompleteTask
{
    public sealed class CompleteTaskCommandHandler
        : IRequestHandler<CompleteTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CompleteTaskCommandHandler(
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            CompleteTaskCommand request,
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

            task.Complete();

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}