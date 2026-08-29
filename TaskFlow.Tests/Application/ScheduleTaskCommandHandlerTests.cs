using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.ScheduleTask;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Tests.Application;

public sealed class ScheduleTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReschedulesOrganizationTask_AfterManageTasksPermissionCheck()
    {
        var task = OrganizationTask();
        var taskRepository = Substitute.For<ITaskRepository>();
        taskRepository.GetByIdAsync(18, Arg.Any<CancellationToken>()).Returns(task);
        var accessGuard = Substitute.For<IOrganizationAccessGuard>();
        var permissionChecker = Substitute.For<IOrganizationPermissionChecker>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(12);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var handler = new ScheduleTaskCommandHandler(
            taskRepository,
            accessGuard,
            permissionChecker,
            currentUser,
            unitOfWork);
        var start = new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new ScheduleTaskCommand(18, start, end),
            CancellationToken.None);

        Assert.Equal(start, task.StartDate);
        Assert.Equal(end, task.ExpectedCompletionDate);
        await permissionChecker.Received(1).EnsurePermissionAsync(
            2,
            12,
            "ManageTasks",
            Arg.Any<CancellationToken>());
        taskRepository.Received(1).Update(task);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DoesNotMutate_WhenPermissionIsDenied()
    {
        var task = OrganizationTask();
        var originalStart = task.StartDate;
        var taskRepository = Substitute.For<ITaskRepository>();
        taskRepository.GetByIdAsync(18, Arg.Any<CancellationToken>()).Returns(task);
        var accessGuard = Substitute.For<IOrganizationAccessGuard>();
        var permissionChecker = Substitute.For<IOrganizationPermissionChecker>();
        permissionChecker
            .EnsurePermissionAsync(2, 12, "ManageTasks", Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ForbiddenException(
                "ORGANIZATION_PERMISSION_REQUIRED",
                "Permission denied.")));
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(12);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new ScheduleTaskCommandHandler(
            taskRepository,
            accessGuard,
            permissionChecker,
            currentUser,
            unitOfWork);

        await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ScheduleTaskCommand(18, originalStart.AddDays(4), originalStart.AddDays(5)),
            CancellationToken.None));

        Assert.Equal(originalStart, task.StartDate);
        taskRepository.DidNotReceive().Update(Arg.Any<TaskEntity>());
        await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_RejectsAnEndBeforeTheStart()
    {
        var validator = new ScheduleTaskCommandValidator();
        var start = new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);

        var result = validator.Validate(
            new ScheduleTaskCommand(18, start, start.AddDays(-1)));

        Assert.Contains(
            result.Errors,
            error => error.PropertyName == nameof(ScheduleTaskCommand.ExpectedCompletionDate));
    }

    private static TaskEntity OrganizationTask() => new(
        "Calendar integration",
        "Make the schedule operable.",
        new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
        TaskPriority.High,
        organizationId: 2,
        createdByUserId: 1,
        expectedCompletionDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
}
