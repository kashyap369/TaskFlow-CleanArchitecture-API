using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Tests.Application;

/// <summary>
/// Phase 7 / P7.2. Each test pins one abuse case the threat model found open, so a later refactor
/// that quietly restores the old behaviour fails here rather than in production.
/// </summary>
public sealed class MeetingSecurityHardeningTests
{
    // ---- Access links ------------------------------------------------------------------------

    [Fact]
    public async Task RevokingAnAccessLink_EndsTheSessionsItAlreadyIssued_AndEjectsTheGuest()
    {
        var (meeting, link, guestAccess, media, session) = ArrangeLinkWithLiveGuest();
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        var handler = new RevokeMeetingAccessLinkCommandHandler(MeetingsReturning(meeting), user,
            Substitute.For<IOrganizationPermissionChecker>(), guestAccess, media,
            Substitute.For<ILogger<RevokeMeetingAccessLinkCommandHandler>>(), Substitute.For<IUnitOfWork>());

        await handler.Handle(new RevokeMeetingAccessLinkCommand(5, link.Id), CancellationToken.None);

        Assert.NotNull(link.RevokedAtUtc);
        Assert.NotNull(session.RevokedAtUtc);
        guestAccess.Received(1).UpdateSession(session);
        await media.Received(1).RemoveParticipantsAsync("meeting-room", "m5-p2-", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotatingAnAccessLink_EndsOldSessions_AndNeverMintsAnAlreadyExpiredReplacement()
    {
        var (meeting, link, guestAccess, media, session) = ArrangeLinkWithLiveGuest();
        // The old window has already lapsed; the replacement must still be usable.
        typeof(MeetingAccessLink).GetProperty(nameof(MeetingAccessLink.ExpiresAtUtc))!
            .SetValue(link, DateTime.UtcNow.AddMinutes(-5));
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        var handler = new RotateMeetingAccessLinkCommandHandler(MeetingsReturning(meeting), user,
            Substitute.For<IOrganizationPermissionChecker>(), guestAccess, media,
            Substitute.For<ILogger<RotateMeetingAccessLinkCommandHandler>>(), Substitute.For<IUnitOfWork>());

        var replacement = await handler.Handle(new RotateMeetingAccessLinkCommand(5, link.Id), CancellationToken.None);

        Assert.NotNull(session.RevokedAtUtc);
        Assert.True(replacement.ExpiresAtUtc > DateTime.UtcNow);
        await media.Received(1).RemoveParticipantsAsync("meeting-room", "m5-p2-", Arg.Any<CancellationToken>());
    }

    private static (Meeting Meeting, MeetingAccessLink Link, IMeetingGuestAccessRepository GuestAccess,
        IMeetingMediaProvider Media, MeetingGuestSession Session) ArrangeLinkWithLiveGuest()
    {
        var meeting = LiveMeeting();
        var guest = meeting.AddGuestParticipant("GUEST@EXAMPLE.TEST", "Guest", MeetingAccessLevel.Participant, null);
        SetId(guest, 2);
        var link = meeting.AddAccessLink(new string('a', 64), MeetingAccessLinkMode.Reusable, null,
            MeetingAccessLevel.Participant, null, DateTime.UtcNow.AddDays(1), null);
        SetId(link, 9);
        var session = new MeetingGuestSession(5, 2, new string('b', 64), DateTime.UtcNow.AddMinutes(30), 9);
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetActiveSessionsForLinkAsync(9, Arg.Any<CancellationToken>()).Returns([session]);
        var media = Substitute.For<IMeetingMediaProvider>(); media.IsEnabled.Returns(true);
        return (meeting, link, guestAccess, media, session);
    }

    // ---- Guest sessions ----------------------------------------------------------------------

    [Fact]
    public async Task RemovedGuest_CannotRenameThemselves()
    {
        var meeting = LiveMeeting();
        var guest = meeting.AddGuestParticipant("GUEST@EXAMPLE.TEST", "Guest", MeetingAccessLevel.Participant, null);
        SetId(guest, 2);
        meeting.UpdateParticipant(2, MeetingAccessLevel.Participant, null, MeetingParticipantState.Removed);
        var handler = new ConfirmMeetingGuestDisplayNameCommandHandler(GuestAccessWithSession(),
            MeetingsReturning(meeting), Substitute.For<IUserRepository>(), Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(
            new ConfirmMeetingGuestDisplayNameCommand("session-token", "Impersonator"), CancellationToken.None));

        Assert.Equal("MEETING_GUEST_ACCESS_REVOKED", error.Code);
        Assert.Equal("Guest", guest.DisplayName);
    }

    [Fact]
    public async Task GuestSession_ForADeletedParticipant_IsRejectedRatherThanCrashing()
    {
        var meeting = LiveMeeting();
        var guest = meeting.AddGuestParticipant("GUEST@EXAMPLE.TEST", "Guest", MeetingAccessLevel.Participant, null);
        SetId(guest, 2); guest.SoftDelete();
        var handler = new GetMeetingGuestSessionQueryHandler(GuestAccessWithSession(),
            MeetingsReturning(meeting), Substitute.For<IUserRepository>());

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(
            new GetMeetingGuestSessionQuery("session-token"), CancellationToken.None));

        Assert.Equal("MEETING_GUEST_SESSION_INVALID", error.Code);
    }

    private static IMeetingGuestAccessRepository GuestAccessWithSession()
    {
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetSessionByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MeetingGuestSession(5, 2, new string('b', 64), DateTime.UtcNow.AddMinutes(30), 9));
        return guestAccess;
    }

    // ---- Chat --------------------------------------------------------------------------------

    [Fact]
    public async Task ReplyingToAMessageFromAnotherMeeting_IsRejected()
    {
        var meeting = LiveMeeting();
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.GetMessageAsync(5, 4242, Arg.Any<CancellationToken>()).Returns((MeetingMessage?)null);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        var handler = new SendMeetingMessageCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, user, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new SendMeetingMessageCommand(5, Guid.NewGuid(), "Hello", 4242), CancellationToken.None));

        Assert.Equal("MEETING_MESSAGE_NOT_FOUND", error.Code);
        await collaboration.DidNotReceive().AddMessageAsync(Arg.Any<MeetingMessage>(), Arg.Any<CancellationToken>());
    }

    // ---- Recording consent -------------------------------------------------------------------

    [Fact]
    public async Task RecordingRequest_AsksEveryoneTheProviderSaysIsInTheRoom_EvenWithNoAttendanceWebhook()
    {
        var meeting = LiveMeeting();
        SetId(meeting.AddRegisteredParticipant(22, MeetingAccessLevel.Participant), 2);
        var media = Substitute.For<IMeetingMediaProvider>(); media.IsEnabled.Returns(true);
        media.ListRoomParticipantIdentitiesAsync("meeting-room", Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<string>>([$"m5-p1-{new string('a', 32)}", $"m5-p2-{new string('b', 32)}"]);

        var result = await RecordingHandler(meeting, media).Handle(
            new RequestMeetingRecordingCommand(5, 60), CancellationToken.None);

        Assert.Contains(result.Consents, x => x.ParticipantId == 2 && x.Status == MeetingRecordingConsentStatus.Pending);
        await media.DidNotReceive().StartRoomRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordingRequest_RefusesWhenTheLiveRosterCannotBeRead()
    {
        var meeting = LiveMeeting();
        var media = Substitute.For<IMeetingMediaProvider>(); media.IsEnabled.Returns(true);
        media.ListRoomParticipantIdentitiesAsync("meeting-room", Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("provider unreachable"));

        var error = await Assert.ThrowsAsync<BusinessException>(() => RecordingHandler(meeting, media)
            .Handle(new RequestMeetingRecordingCommand(5, 60), CancellationToken.None));

        Assert.Equal("MEETING_RECORDING_ROSTER_UNAVAILABLE", error.Code);
        await media.DidNotReceive().StartRoomRecordingAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static RequestMeetingRecordingCommandHandler RecordingHandler(Meeting meeting, IMeetingMediaProvider media)
    {
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(11);
        return new RequestMeetingRecordingCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), Substitute.For<IMeetingRecordingRepository>(),
            user, media, Substitute.For<IUnitOfWork>(),
            Substitute.For<ILogger<RequestMeetingRecordingCommandHandler>>());
    }

    [Fact]
    public async Task ParticipantWhoWasNeverAsked_CannotVetoARecording()
    {
        var meeting = LiveMeeting();
        SetId(meeting.AddRegisteredParticipant(22, MeetingAccessLevel.Participant), 2);
        var recording = new MeetingRecording(5, 1, "meetings/5/recordings/a.mp4", [1], DateTime.UtcNow.AddMinutes(1));
        recording.RecordConsent(1, true, DateTime.UtcNow);
        var recordings = Substitute.For<IMeetingRecordingRepository>();
        recordings.GetByIdAsync(5, 3, Arg.Any<CancellationToken>()).Returns(recording);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(22);
        var handler = new ConsentMeetingRecordingCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), recordings, user,
            Substitute.For<IMeetingMediaProvider>(), Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(
            new ConsentMeetingRecordingCommand(5, 3, false), CancellationToken.None));

        Assert.Equal("MEETING_RECORDING_CONSENT_NOT_REQUESTED", error.Code);
        Assert.NotEqual(MeetingRecordingStatus.Failed, recording.Status);
    }

    [Fact]
    public async Task LateJoiner_MayStillAcceptConsentSoTheJoinGateCanLetThemIn()
    {
        var meeting = LiveMeeting();
        SetId(meeting.AddRegisteredParticipant(22, MeetingAccessLevel.Participant), 2);
        var recording = new MeetingRecording(5, 1, "meetings/5/recordings/a.mp4", [1], DateTime.UtcNow.AddMinutes(1));
        recording.RecordConsent(1, true, DateTime.UtcNow);
        recording.BeginStarting("egress-1", DateTime.UtcNow);
        var recordings = Substitute.For<IMeetingRecordingRepository>();
        recordings.GetByIdAsync(5, 3, Arg.Any<CancellationToken>()).Returns(recording);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(22);
        var handler = new ConsentMeetingRecordingCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), recordings, user,
            Substitute.For<IMeetingMediaProvider>(), Substitute.For<IUnitOfWork>());

        await handler.Handle(new ConsentMeetingRecordingCommand(5, 3, true), CancellationToken.None);

        Assert.True(recording.HasAcceptedConsent(2));
    }

    // ---- Shared arrangement -------------------------------------------------------------------

    private static Meeting LiveMeeting()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-room",
            true, true, true, true, true, false, 90);
        SetId(meeting, 5); SetId(meeting.Participants.Single(), 1); meeting.Start(DateTime.UtcNow);
        return meeting;
    }

    private static IMeetingRepository MeetingsReturning(Meeting meeting)
    {
        var meetings = Substitute.For<IMeetingRepository>();
        meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        return meetings;
    }

    private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);
}
