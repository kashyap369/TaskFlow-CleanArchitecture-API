using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StopWorkLog
{
    public sealed class StopWorkLogCommandHandler
        : IRequestHandler<StopWorkLogCommand>
    {
        private readonly ITaskWorkLogRepository _taskWorkLogRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public StopWorkLogCommandHandler(
            ITaskWorkLogRepository taskWorkLogRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskWorkLogRepository = taskWorkLogRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(
            StopWorkLogCommand request,
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

            // A user can only stop their own timer.
            if (workLog.UserId != _currentUserService.UserId)
            {
                throw new ForbiddenException(
                    "WORK_LOG_FORBIDDEN",
                    "You can only stop your own work log.");
            }

            workLog.Stop(request.Notes);

            _taskWorkLogRepository.Update(workLog);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
    }
}
