using MediatR;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Application.Features.WorkManagement.Tasks.Commands.SetTaskEstimate;

public sealed class SetTaskEstimateCommandHandler : IRequestHandler<SetTaskEstimateCommand>
{
    private readonly ITaskRepository _tasks;
    private readonly IOrganizationAccessGuard _accessGuard;
    private readonly IOrganizationPermissionChecker _permissionChecker;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetTaskEstimateCommandHandler(
        ITaskRepository tasks,
        IOrganizationAccessGuard accessGuard,
        IOrganizationPermissionChecker permissionChecker,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _tasks = tasks;
        _accessGuard = accessGuard;
        _permissionChecker = permissionChecker;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SetTaskEstimateCommand request, CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureTaskAsync(request.TaskId, cancellationToken);
        var task = await _tasks.GetByIdAsync(request.TaskId, cancellationToken);
        if (task is null)
            throw new NotFoundException("TASK_NOT_FOUND", "Task not found.");
        if (!task.OrganizationId.HasValue)
            throw new BusinessException(
                "TASK_ESTIMATE_REQUIRES_ORGANIZATION",
                "Capacity estimates are available only for organization tasks.");

        await _permissionChecker.EnsurePermissionAsync(
            task.OrganizationId.Value,
            _currentUser.UserId,
            OrganizationPermissionNames.ManageTasks,
            cancellationToken);

        task.SetEstimate(request.EstimateMinutes);
        _tasks.Update(task);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
