using MediatR;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.UpdateSubTask
{
    public sealed class UpdateSubTaskCommandHandler
        : IRequestHandler<UpdateSubTaskCommand>
    {
        private readonly ISubTaskRepository _subTaskRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSubTaskCommandHandler(
            ISubTaskRepository subTaskRepository,
            IUnitOfWork unitOfWork)
        {
            _subTaskRepository = subTaskRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            UpdateSubTaskCommand request,
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

            var existingSubTask =
                await _subTaskRepository.GetByTitleAsync(
                    subTask.TaskId,
                    request.Title,
                    cancellationToken);

            if (existingSubTask is not null &&
                existingSubTask.Id != subTask.Id)
            {
                throw new ConflictException(
                    "SUBTASK_ALREADY_EXISTS",
                    "SubTask with same title already exists.");
            }

            subTask.UpdateTitle(
                request.Title);

            _subTaskRepository.Update(
                subTask);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}