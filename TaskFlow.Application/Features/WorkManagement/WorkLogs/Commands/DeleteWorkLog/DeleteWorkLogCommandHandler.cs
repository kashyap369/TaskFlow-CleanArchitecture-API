using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.DeleteWorkLog
{
    public sealed class DeleteWorkLogCommandHandler
        : IRequestHandler<DeleteWorkLogCommand>
    {
        private readonly ITaskWorkLogRepository _taskWorkLogRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteWorkLogCommandHandler(
            ITaskWorkLogRepository taskWorkLogRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskWorkLogRepository = taskWorkLogRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            DeleteWorkLogCommand request,
            CancellationToken cancellationToken)
        {
            var workLog =
                await _taskWorkLogRepository.GetByIdAsync(
                    request.WorkLogId,
                    cancellationToken);

            if (workLog is null)
            {
                throw new NotFoundException(
                    "WORK_LOG_NOT_FOUND",
                    "Work log not found.");
            }

            if (workLog.UserId != _currentUserService.UserId)
            {
                throw new ForbiddenException(
                    "WORK_LOG_FORBIDDEN",
                    "You can only delete your own work log.");
            }

            _taskWorkLogRepository.Remove(workLog);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
