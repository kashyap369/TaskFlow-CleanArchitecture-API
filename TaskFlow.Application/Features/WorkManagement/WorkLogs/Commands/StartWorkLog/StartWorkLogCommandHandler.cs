using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.StartWorkLog
{
    public sealed class StartWorkLogCommandHandler
        : IRequestHandler<StartWorkLogCommand, int>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ITaskWorkLogRepository _taskWorkLogRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public StartWorkLogCommandHandler(
            ITaskRepository taskRepository,
            ITaskWorkLogRepository taskWorkLogRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _taskWorkLogRepository = taskWorkLogRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(
            StartWorkLogCommand request,
            CancellationToken cancellationToken)
        {
            var taskExists =
                await _taskRepository.ExistsAsync(
                    request.TaskId,
                    cancellationToken);

            if (!taskExists)
            {
                throw new NotFoundException(
                    "TASK_NOT_FOUND",
                    "Task not found.");
            }

            var userId = _currentUserService.UserId;

            // Only one timer can run at a time per user.
            var running =
                await _taskWorkLogRepository
                    .GetRunningByUserIdAsync(
                        userId,
                        cancellationToken);

            if (running is not null)
            {
                throw new ConflictException(
                    "WORK_LOG_ALREADY_RUNNING",
                    "You already have a running work log. Stop it first.");
            }

            var workLog =
                TaskWorkLog.StartNew(
                    request.TaskId,
                    userId,
                    request.Notes);

            await _taskWorkLogRepository.AddAsync(
                workLog,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(
                cancellationToken);

            return workLog.Id;
        }
    }
}
