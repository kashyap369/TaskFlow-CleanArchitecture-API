using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.WorkManagement.WorkLogs;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.WorkLogs.Commands.LogManualWork
{
    public sealed class LogManualWorkCommandHandler
        : IRequestHandler<LogManualWorkCommand, int>
    {
        private readonly ITaskRepository _taskRepository;
        private readonly ITaskWorkLogRepository _taskWorkLogRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;

        public LogManualWorkCommandHandler(
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
            LogManualWorkCommand request,
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

            var workLog =
                TaskWorkLog.LogManual(
                    request.TaskId,
                    _currentUserService.UserId,
                    request.StartedAt,
                    request.EndedAt,
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
