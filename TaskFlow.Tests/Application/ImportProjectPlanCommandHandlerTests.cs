using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.ImportProjectPlan;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Enums.WorkManagement;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Domain.ValueObjects;
using TaskEntity = TaskFlow.Domain.Entities.WorkManagement.Tasks.Task;

namespace TaskFlow.Tests.Application;

public sealed class ImportProjectPlanCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesPersonalProjectTasksAndSubTasksInsideOneTransaction()
    {
        var projects = Substitute.For<IProjectRepository>();
        projects.ExistsPersonalByNameAsync(7, "Launch", Arg.Any<CancellationToken>()).Returns(false);
        projects.AddAsync(Arg.Any<TaskFlow.Domain.Entities.WorkManagement.Projects.Project>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SetId(call.Arg<TaskFlow.Domain.Entities.WorkManagement.Projects.Project>(), 41);
                return Task.CompletedTask;
            });
        var tasks = Substitute.For<ITaskRepository>();
        tasks.GetByCreatedByUserIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TaskEntity>());
        var nextTaskId = 100;
        tasks.AddAsync(Arg.Any<TaskEntity>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                SetId(call.Arg<TaskEntity>(), nextTaskId++);
                return Task.CompletedTask;
            });
        var subTasks = Substitute.For<ISubTaskRepository>();
        var users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(User.Register(
            new FullName("Plan", "Owner"), new Email("owner@example.test"),
            new PhoneNumber("9999999999"), "password", AccountType.Individual));
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(7);
        var plannerBoards = Substitute.For<IPlannerBoardRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        unitOfWork.ExecuteInTransactionAsync(
                Arg.Any<Func<CancellationToken, Task<ImportProjectPlanResult>>>(),
                Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<Func<CancellationToken, Task<ImportProjectPlanResult>>>()(CancellationToken.None));

        var handler = new ImportProjectPlanCommandHandler(
            projects, tasks, subTasks, users,
            Substitute.For<IOrganizationMemberRepository>(),
            Substitute.For<ITeamRepository>(),
            Substitute.For<IOrganizationAccessGuard>(),
            Substitute.For<IOrganizationPermissionChecker>(),
            currentUser, plannerBoards, unitOfWork);
        var start = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);

        var result = await handler.Handle(new ImportProjectPlanCommand(
            "Launch", "Complete the launch", start, start.AddDays(20), null,
            [
                new("T-1", "Design", "", start, start.AddDays(5), TaskPriority.High, 480, null, null,
                    ["Wireframe", "Approve design"]),
                new("T-2", "Build", "", start.AddDays(6), start.AddDays(18), TaskPriority.Medium, 1200, null, null,
                    ["Implement"]),
            ]), CancellationToken.None);

        Assert.Equal(41, result.ProjectId);
        Assert.Equal(2, result.TaskCount);
        Assert.Equal(3, result.SubTaskCount);
        await tasks.Received(2).AddAsync(Arg.Any<TaskEntity>(), Arg.Any<CancellationToken>());
        await subTasks.Received(3).AddAsync(
            Arg.Any<TaskFlow.Domain.Entities.WorkManagement.SubTasks.SubTask>(),
            Arg.Any<CancellationToken>());
        await plannerBoards.Received(1).AddAsync(Arg.Any<PlannerBoard>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).ExecuteInTransactionAsync(
            Arg.Any<Func<CancellationToken, Task<ImportProjectPlanResult>>>(),
            Arg.Any<CancellationToken>());
    }

    private static void SetId(BaseEntity entity, int id)
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity, id);
    }
}
