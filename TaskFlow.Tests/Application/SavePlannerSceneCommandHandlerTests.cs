using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Planner.Commands.SavePlannerScene;
using TaskFlow.Domain.Entities.Planner;
using TaskFlow.Domain.Entities.WorkManagement.Projects;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.Interfaces.Planner;
using TaskFlow.Domain.Interfaces.WorkManagement;

namespace TaskFlow.Tests.Application;

public sealed class SavePlannerSceneCommandHandlerTests
{
    [Fact]
    public async Task Handle_SavesAgainstTheExpectedRevision()
    {
        var fixture = Fixture(ownerUserId: 7);

        var result = await fixture.Handler.Handle(
            new SavePlannerSceneCommand(42, 0, PlannerSceneDocument.Empty),
            CancellationToken.None);

        Assert.Equal(1, result.Revision);
        await fixture.BoardRepository.Received(1).AddRevisionAsync(
            Arg.Is<PlannerSceneRevision>(x => x.RevisionNumber == 1),
            Arg.Any<CancellationToken>());
        await fixture.BoardRepository.Received(1).PruneRevisionsAsync(
            fixture.Board.Id,
            1,
            PlannerSceneDocument.RevisionRetentionLimit,
            Arg.Any<CancellationToken>());
        fixture.BoardRepository.Received(1).Update(fixture.Board);
        await fixture.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReturnsConflictForAStaleTab()
    {
        var fixture = Fixture(ownerUserId: 7);
        fixture.Board.SaveScene(PlannerSceneDocument.Empty, 0, 7);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            fixture.Handler.Handle(
                new SavePlannerSceneCommand(42, 0, PlannerSceneDocument.Empty),
                CancellationToken.None));

        Assert.Equal("PLANNER_REVISION_CONFLICT", exception.Code);
    }

    [Fact]
    public async Task Handle_DeniesAnotherUsersPersonalProject()
    {
        var fixture = Fixture(ownerUserId: 8, currentUserId: 7);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            fixture.Handler.Handle(
                new SavePlannerSceneCommand(42, 0, PlannerSceneDocument.Empty),
                CancellationToken.None));
    }

    private static TestFixture Fixture(int ownerUserId, int? currentUserId = null)
    {
        var project = new Project(
            "Private plan",
            string.Empty,
            DateTime.UtcNow,
            organizationId: null,
            createdByUserId: ownerUserId);
        var board = PlannerBoard.Create(project, ownerUserId);

        var projectRepository = Substitute.For<IProjectRepository>();
        projectRepository
            .GetByIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(project);

        var boardRepository = Substitute.For<IPlannerBoardRepository>();
        boardRepository
            .GetSceneByProjectIdAsync(42, Arg.Any<CancellationToken>())
            .Returns(board);

        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(currentUserId ?? ownerUserId);

        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        var handler = new SavePlannerSceneCommandHandler(
            boardRepository,
            projectRepository,
            currentUser,
            unitOfWork);

        return new TestFixture(handler, board, boardRepository, unitOfWork);
    }

    private sealed record TestFixture(
        SavePlannerSceneCommandHandler Handler,
        PlannerBoard Board,
        IPlannerBoardRepository BoardRepository,
        IUnitOfWork UnitOfWork);
}
