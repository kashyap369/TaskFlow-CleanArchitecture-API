using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.WorkManagement.Projects.Commands.CreatePersonalProject;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Tests.Application;

public sealed class CreatePersonalProjectCommandHandlerTests
{
    [Fact]
    public async Task Handle_CreatesAPrimaryPlannerBoardForEveryNewPersonalProject()
    {
        var projectRepository = Substitute.For<IProjectRepository>();
        projectRepository
            .ExistsPersonalByNameAsync(7, "Cloud plan", Arg.Any<CancellationToken>())
            .Returns(false);
        var userRepository = Substitute.For<IUserRepository>();
        userRepository
            .GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(User.Register(
                new FullName("Planner", "Owner"),
                new Email("planner-owner@example.test"),
                new PhoneNumber("9999999999"),
                "test-password-hash",
                AccountType.Individual));
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(7);
        var boardRepository = Substitute.For<IPlannerBoardRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new CreatePersonalProjectCommandHandler(
            projectRepository,
            userRepository,
            currentUser,
            boardRepository,
            unitOfWork);

        await handler.Handle(
            new CreatePersonalProjectCommand(
                "Cloud plan",
                "",
                DateTime.UtcNow,
                null),
            CancellationToken.None);

        await projectRepository.Received(1).AddAsync(
            Arg.Is<TaskFlow.Domain.Entities.WorkManagement.Projects.Project>(project =>
                project.IsPersonal && project.CreatedByUserId == 7),
            Arg.Any<CancellationToken>());
        await boardRepository.Received(1).AddAsync(
            Arg.Is<PlannerBoard>(board => board.OwnerUserId == 7 && board.CurrentRevision == 0),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
