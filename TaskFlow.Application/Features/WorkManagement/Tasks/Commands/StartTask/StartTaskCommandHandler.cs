using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.StartTask
{
    public sealed class StartTaskCommandHandler
        : IRequestHandler<StartTaskCommand>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public StartTaskCommandHandler(
            ITaskRepository taskRepository,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            StartTaskCommand request,
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

            task.Start();

            _taskRepository.Update(task);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}