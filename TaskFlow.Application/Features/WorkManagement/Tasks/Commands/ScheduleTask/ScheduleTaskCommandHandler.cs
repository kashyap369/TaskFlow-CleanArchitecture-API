using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ScheduleTask;

public sealed class ScheduleTaskCommandHandler
    : IRequestHandler<ScheduleTaskCommand>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IOrganizationAccessGuard _accessGuard;
    private readonly IOrganizationPermissionChecker _permissionChecker;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleTaskCommandHandler(
        ITaskRepository taskRepository,
        IOrganizationAccessGuard accessGuard,
        IOrganizationPermissionChecker permissionChecker,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository;
        _accessGuard = accessGuard;
        _permissionChecker = permissionChecker;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(
        ScheduleTaskCommand request,
        CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureTaskAsync(
            request.TaskId,
            cancellationToken);

        var task = await _taskRepository.GetByIdAsync(
            request.TaskId,
            cancellationToken);

        if (task is null)
        {
            throw new NotFoundException(
                "TASK_NOT_FOUND",
                "Task not found.");
        }

        if (!task.OrganizationId.HasValue)
        {
            throw new BusinessException(
                "TASK_SCHEDULE_REQUIRES_ORGANIZATION",
                "Calendar scheduling is available only for organization tasks.");
        }

        await _permissionChecker.EnsurePermissionAsync(
            task.OrganizationId.Value,
            _currentUserService.UserId,
            OrganizationPermissionNames.ManageTasks,
            cancellationToken);

        task.Reschedule(
            request.StartDate,
            request.ExpectedCompletionDate);

        _taskRepository.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
