using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.SubTasks.Commands.UpdateSubTask
{
    public sealed class UpdateSubTaskCommandHandler
        : IRequestHandler<UpdateSubTaskCommand>
    {
        private readonly ISubTaskRepository _subTaskRepository;
        private readonly IOrganizationAccessGuard _accessGuard;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateSubTaskCommandHandler(
            ISubTaskRepository subTaskRepository,
            IOrganizationAccessGuard accessGuard,
            IUnitOfWork unitOfWork)
        {
            _subTaskRepository = subTaskRepository;
            _accessGuard = accessGuard;
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

            // The parent task decides who may touch this subtask:
            // org task -> owner/active member; personal task -> its creator.
            await _accessGuard.EnsureTaskAsync(
                subTask.TaskId,
                cancellationToken);

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