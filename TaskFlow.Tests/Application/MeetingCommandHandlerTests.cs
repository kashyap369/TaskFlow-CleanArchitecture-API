using NSubstitute;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
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

    [Fact]
    public async Task JoinToken_DeniesMeetingManagerWhoIsNotAssigned()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-test",
            true, false, true, true, true, false, 90);
        meeting.Start(DateTime.UtcNow);
        var meetings = Substitute.For<IMeetingRepository>();
        meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(22); user.Email.Returns("manager@example.test");
        var media = Substitute.For<IMeetingMediaProvider>();
        var recordings = Substitute.For<IMeetingRecordingRepository>();
        var handler = new GetMeetingJoinTokenCommandHandler(meetings, user, media, recordings);

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetMeetingJoinTokenCommand(5), CancellationToken.None));

        Assert.Equal("MEETING_ROOM_ACCESS_DENIED", error.Code);
        media.DidNotReceive().CreateJoinToken(Arg.Any<MeetingJoinTokenRequest>());
    }

    [Fact]
    public async Task HostRemoval_RevokesParticipantAndDisconnectsEveryLiveIdentity()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-room",
            true, false, true, true, true, false, 90);
        SetId(meeting, 5); var host = meeting.Participants.Single(); SetId(host, 1);
        var target = meeting.AddRegisteredParticipant(22, MeetingAccessLevel.Participant); SetId(target, 2);
        meeting.Start(DateTime.UtcNow);
        var meetings = Substitute.For<IMeetingRepository>(); meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetActiveSessionsAsync(2, Arg.Any<CancellationToken>()).Returns([]);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        var media = Substitute.For<IMeetingMediaProvider>(); var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new RemoveMeetingRoomParticipantCommandHandler(meetings, guestAccess, user, media, unitOfWork);

        await handler.Handle(new RemoveMeetingRoomParticipantCommand(5, 2), CancellationToken.None);

        Assert.Equal(MeetingParticipantState.Removed, target.State);
        await media.Received(1).RemoveParticipantsAsync("meeting-room", "m5-p2-", Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProviderWebhook_ReconcilesAttendanceIdempotently()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-room",
            true, false, true, true, true, false, 90);
        SetId(meeting, 5); var host = meeting.Participants.Single(); SetId(host, 1); meeting.Start(DateTime.UtcNow);
        var meetings = Substitute.For<IMeetingRepository>(); meetings.GetByRoomNameAsync("meeting-room", Arg.Any<CancellationToken>()).Returns(meeting);
        meetings.HasWebhookReceiptAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var unitOfWork = Substitute.For<IUnitOfWork>(); var recordings = Substitute.For<IMeetingRecordingRepository>();
        var handler = new ProcessMeetingProviderWebhookCommandHandler(meetings, recordings, unitOfWork);
        var identity = $"m5-p1-{new string('a', 32)}"; var joined = DateTimeOffset.UtcNow.AddMinutes(-2); var left = DateTimeOffset.UtcNow;

        await handler.Handle(new ProcessMeetingProviderWebhookCommand(new MeetingProviderWebhook("e2", "participant_left", "meeting-room", identity, "sid-1", left)), CancellationToken.None);
        await handler.Handle(new ProcessMeetingProviderWebhookCommand(new MeetingProviderWebhook("e1", "participant_joined", "meeting-room", identity, "sid-1", joined)), CancellationToken.None);
        await handler.Handle(new ProcessMeetingProviderWebhookCommand(new MeetingProviderWebhook("e1", "participant_joined", "meeting-room", identity, "sid-1", joined)), CancellationToken.None);

        var interval = Assert.Single(meeting.Attendance); Assert.Equal(joined.UtcDateTime, interval.JoinedAtUtc); Assert.Equal(left.UtcDateTime, interval.LeftAtUtc);
        await meetings.Received(3).AddWebhookReceiptAsync(Arg.Any<MeetingWebhookReceipt>(), Arg.Any<CancellationToken>());
    }

    private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);
}
