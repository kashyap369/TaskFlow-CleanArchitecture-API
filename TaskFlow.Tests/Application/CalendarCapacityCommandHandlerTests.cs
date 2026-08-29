using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Organizations.OrganizationMember.Commands.SetMemberCapacity;
using TaskFlow.Application.Features.Reporting.Queries.GetOrganizationCapacity;
using TaskFlow.Application.Features.WorkManagement.Tasks.Commands.SetTaskEstimate;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Tests.Application;

public sealed class CalendarCapacityCommandHandlerTests
{
    [Fact]
    public async Task SetTaskEstimate_Persists_AfterManageTasksPermissionCheck()
    {
        var task = new TaskEntity(
            "Estimate capacity work",
            "",
            DateTime.UtcNow,
            TaskPriority.High,
            organizationId: 7,
            createdByUserId: 4);
        var tasks = Substitute.For<ITaskRepository>();
        tasks.GetByIdAsync(18, Arg.Any<CancellationToken>()).Returns(task);
        var accessGuard = Substitute.For<IOrganizationAccessGuard>();
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(4);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var handler = new SetTaskEstimateCommandHandler(
            tasks, accessGuard, permissions, currentUser, unitOfWork);

        await handler.Handle(new SetTaskEstimateCommand(18, 360), CancellationToken.None);

        Assert.Equal(360, task.EstimateMinutes);
        await permissions.Received(1).EnsurePermissionAsync(
            7, 4, "ManageTasks", Arg.Any<CancellationToken>());
        tasks.Received(1).Update(task);
    }

    [Fact]
    public async Task SetMemberCapacity_Persists_AfterManageMembersPermissionCheck()
    {
        var member = new OrganizationMember(7, 22, 3);
        var members = Substitute.For<IOrganizationMemberRepository>();
        members.GetMemberAsync(7, 22, Arg.Any<CancellationToken>()).Returns(member);
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(4);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        var handler = new SetMemberCapacityCommandHandler(
            members, permissions, currentUser, unitOfWork);

        await handler.Handle(new SetMemberCapacityCommand(7, 22, 2_100), CancellationToken.None);

        Assert.Equal(2_100, member.WeeklyCapacityMinutes);
        await permissions.Received(1).EnsurePermissionAsync(
            7, 4, "ManageMembers", Arg.Any<CancellationToken>());
        members.Received(1).Update(member);
    }

    [Fact]
    public void Validators_RejectImpossibleValues_AndNonMondayWeeks()
    {
        var estimate = new SetTaskEstimateCommandValidator()
            .Validate(new SetTaskEstimateCommand(18, -1));
        var capacity = new SetMemberCapacityCommandValidator()
            .Validate(new SetMemberCapacityCommand(7, 22, 10_081));
        var query = new GetOrganizationCapacityQueryValidator()
            .Validate(new GetOrganizationCapacityQuery(
                7,
                new DateOnly(2026, 8, 30),
                13));

        Assert.False(estimate.IsValid);
        Assert.False(capacity.IsValid);
        Assert.Contains(query.Errors, error => error.PropertyName == "WeekStart");
        Assert.Contains(query.Errors, error => error.PropertyName == "Weeks");
    }

    [Fact]
    public void DomainValues_CanBeCleared_WithoutInventingAvailability()
    {
        var task = new TaskEntity(
            "Unestimated work", "", DateTime.UtcNow, TaskPriority.Low, 7, 4);
        var member = new OrganizationMember(7, 22, 3);

        task.SetEstimate(60);
        member.SetWeeklyCapacity(2_400);
        task.SetEstimate(null);
        member.SetWeeklyCapacity(null);

        Assert.Null(task.EstimateMinutes);
        Assert.Null(member.WeeklyCapacityMinutes);
    }
}
