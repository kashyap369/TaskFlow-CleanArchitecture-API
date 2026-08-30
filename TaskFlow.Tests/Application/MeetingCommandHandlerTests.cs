using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Tests.Application;

public sealed class MeetingCommandHandlerTests
{
    [Fact]
    public async Task Create_RequiresCreatePermission_AndPersistsCreatorAsHost()
    {
        var meetings = Substitute.For<IMeetingRepository>();
        var members = Substitute.For<IOrganizationMemberRepository>();
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new CreateMeetingCommandHandler(meetings, members, permissions, user, unitOfWork);

        await handler.Handle(new CreateMeetingCommand(7, "Planning review", null, null, null), CancellationToken.None);

        await permissions.Received(1).EnsurePermissionAsync(7, 11,
            OrganizationPermissionNames.CreateMeetings, Arg.Any<CancellationToken>());
        await meetings.Received(1).AddAsync(Arg.Is<Meeting>(meeting =>
            meeting.CreatedByUserId == 11 && meeting.Participants.Single().UserId == 11),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AllowsCreator_AndRequiresManagePermissionForAnotherUser()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-test",
            true, false, true, true, true, false, 90);
        var meetings = Substitute.For<IMeetingRepository>();
        meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(22);
        var permissions = Substitute.For<IOrganizationPermissionChecker>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new UpdateMeetingCommandHandler(meetings, user, permissions, unitOfWork);

        await handler.Handle(new UpdateMeetingCommand(5, "Updated review", null, null, null), CancellationToken.None);

        await permissions.Received(1).EnsurePermissionAsync(7, 22,
            OrganizationPermissionNames.ManageMeetings, Arg.Any<CancellationToken>());
        Assert.Equal("Updated review", meeting.Title);
    }
}
