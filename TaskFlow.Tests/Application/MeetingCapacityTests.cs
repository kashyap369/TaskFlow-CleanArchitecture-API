using Microsoft.Extensions.Logging;
using NSubstitute;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Contracts.Storage;
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
/// Phase 7 / P7.3. TaskFlow declares meeting capacity instead of implying unlimited scale, so every
/// declared ceiling has to be refused by the server. Each test here pins one ceiling and the code
/// the person who hit it receives, because the UI shows the server's message and nothing else.
/// </summary>
[Collection(TaskFlow.Tests.Application.MeetingTelemetryCollection.Name)]
public sealed class MeetingCapacityTests
{
    // ---- Participants ---------------------------------------------------------------------------

    [Fact]
    public async Task AddingAParticipant_IsRefusedOnceTheRosterIsFull()
    {
        var meeting = DraftMeeting();
        FillRoster(meeting, seats: 3);
        var members = Substitute.For<IOrganizationMemberRepository>();
        members.IsActiveMemberAsync(7, 99, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new AddMeetingParticipantCommandHandler(MeetingsReturning(meeting), members,
            HostUser(), Substitute.For<IOrganizationPermissionChecker>(),
            new MeetingTestPolicy { MaxParticipantsPerMeeting = 3 }, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new AddMeetingParticipantCommand(5, 99), CancellationToken.None));

        Assert.Equal("MEETING_PARTICIPANT_LIMIT_REACHED", error.Code);
        Assert.Contains("3", error.Message);
        Assert.Equal(3, meeting.ActiveParticipantCount);
    }

    [Fact]
    public async Task CreatingAMeeting_IsRefusedWhenItsOwnParticipantListExceedsTheCeiling()
    {
        var meetings = Substitute.For<IMeetingRepository>();
        var members = Substitute.For<IOrganizationMemberRepository>();
        members.IsActiveMemberAsync(7, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateMeetingCommandHandler(meetings, members,
            Substitute.For<IOrganizationPermissionChecker>(), HostUser(),
            new MeetingTestPolicy { MaxParticipantsPerMeeting = 3 }, Substitute.For<IUnitOfWork>());

        // The creator already holds a seat, so three more would be four.
        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new CreateMeetingCommand(7, "Все руки", null, null, null,
                ParticipantUserIds: [21, 22, 23]), CancellationToken.None));

        Assert.Equal("MEETING_PARTICIPANT_LIMIT_REACHED", error.Code);
        await meetings.DidNotReceive().AddAsync(Arg.Any<Meeting>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void RemovingAParticipant_ReturnsTheirSeatToTheMeeting()
    {
        var meeting = DraftMeeting();
        var participant = meeting.AddRegisteredParticipant(21, MeetingAccessLevel.Participant);
        SetId(participant, 2);
        Assert.Equal(2, meeting.ActiveParticipantCount);

        meeting.UpdateParticipant(2, MeetingAccessLevel.Participant, null, MeetingParticipantState.Removed);

        // A removed person cannot return without a new decision, so holding their seat forever would
        // shrink every long-running meeting until nobody could be added.
        Assert.Equal(1, meeting.ActiveParticipantCount);
        meeting.EnsureParticipantCapacity(2);
    }

    [Fact]
    public async Task GuestVerification_IsRefusedWhenFull_ButAnEmailThatAlreadyHasASeatStillGetsIn()
    {
        var meeting = LiveMeeting();
        var existing = meeting.AddGuestParticipant("GUEST@EXAMPLE.TEST", "Guest", MeetingAccessLevel.Participant, null);
        SetId(existing, 2);
        var policy = new MeetingTestPolicy { MaxParticipantsPerMeeting = 2 };

        // The returning guest is re-admitted: they are not a new seat.
        var returning = await VerifyGuest(meeting, policy, "guest@example.test");
        Assert.Equal(2, returning.Session.ParticipantId);

        var error = await Assert.ThrowsAsync<BusinessException>(() => VerifyGuest(meeting, policy, "stranger@example.test"));
        Assert.Equal("MEETING_PARTICIPANT_LIMIT_REACHED", error.Code);
        Assert.Equal(2, meeting.ActiveParticipantCount);
    }

    // ---- Simultaneous meetings ------------------------------------------------------------------

    [Fact]
    public async Task StartingAMeeting_IsRefusedWhenTheOrganizationIsAlreadyAtItsLiveLimit()
    {
        var meeting = DraftMeeting();
        var meetings = MeetingsReturning(meeting);
        meetings.CountLiveAsync(7, Arg.Any<CancellationToken>()).Returns(2);
        var handler = new StartMeetingCommandHandler(meetings, HostUser(),
            Substitute.For<IOrganizationPermissionChecker>(),
            new MeetingTestPolicy { MaxConcurrentLiveMeetingsPerOrganization = 2 }, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new StartMeetingCommand(5), CancellationToken.None));

        Assert.Equal("MEETING_CONCURRENT_LIMIT_REACHED", error.Code);
        // The refusal must leave the meeting startable once capacity frees up.
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
    }

    [Fact]
    public async Task StartingAMeeting_IsAllowedWhileTheOrganizationIsBelowItsLiveLimit()
    {
        var meeting = DraftMeeting();
        var meetings = MeetingsReturning(meeting);
        meetings.CountLiveAsync(7, Arg.Any<CancellationToken>()).Returns(1);
        var handler = new StartMeetingCommandHandler(meetings, HostUser(),
            Substitute.For<IOrganizationPermissionChecker>(),
            new MeetingTestPolicy { MaxConcurrentLiveMeetingsPerOrganization = 2 }, Substitute.For<IUnitOfWork>());

        await handler.Handle(new StartMeetingCommand(5), CancellationToken.None);

        Assert.Equal(MeetingStatus.Live, meeting.Status);
    }

    // ---- Chat ------------------------------------------------------------------------------------

    [Fact]
    public async Task SendingAMessage_IsRefusedOnceTheMeetingHasReachedItsMessageLimit()
    {
        var meeting = LiveMeeting();
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.CountMessagesAsync(5, Arg.Any<CancellationToken>()).Returns(50);
        var handler = new SendMeetingMessageCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, HostUser(),
            new MeetingTestPolicy { MaxMessagesPerMeeting = 50 }, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new SendMeetingMessageCommand(5, Guid.NewGuid(), "One more"), CancellationToken.None));

        Assert.Equal("MEETING_MESSAGE_LIMIT_REACHED", error.Code);
        await collaboration.DidNotReceive().AddMessageAsync(Arg.Any<MeetingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ARetryOfAMessageThatAlreadyLanded_StillSucceedsAtTheMessageLimit()
    {
        var meeting = LiveMeeting();
        var clientMessageId = Guid.NewGuid();
        var stored = new MeetingMessage(5, 1, clientMessageId, "Already here", null);
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.CountMessagesAsync(5, Arg.Any<CancellationToken>()).Returns(50);
        collaboration.GetMessageByClientIdAsync(5, 1, clientMessageId, Arg.Any<CancellationToken>()).Returns(stored);
        var handler = new SendMeetingMessageCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, HostUser(),
            new MeetingTestPolicy { MaxMessagesPerMeeting = 50 }, Substitute.For<IUnitOfWork>());

        // Refusing a retry would make the client believe a delivered message was lost.
        var result = await handler.Handle(new SendMeetingMessageCommand(5, clientMessageId, "Already here"), CancellationToken.None);

        Assert.Equal(clientMessageId, result.ClientMessageId);
    }

    [Fact]
    public async Task TwoSendsOfOneClientMessageIdInFlightAtOnce_ReportTheSameMessageRatherThanFailing()
    {
        var meeting = LiveMeeting();
        var clientMessageId = Guid.NewGuid();
        var winner = new MeetingMessage(5, 1, clientMessageId, "Race", null);
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        // Both requests looked before either wrote, so this one sees nothing and then loses the
        // unique index; by the time it asks again, the winner is committed.
        collaboration.GetMessageByClientIdAsync(5, 1, clientMessageId, Arg.Any<CancellationToken>())
            .Returns((MeetingMessage?)null, winner);
        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("duplicate key value violates unique constraint"));
        var handler = new SendMeetingMessageCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, HostUser(),
            new MeetingTestPolicy(), uow);

        var result = await handler.Handle(new SendMeetingMessageCommand(5, clientMessageId, "Race"), CancellationToken.None);

        Assert.Equal(clientMessageId, result.ClientMessageId);
    }

    [Fact]
    public async Task AWriteFailureThatIsNotADuplicate_IsStillReported()
    {
        var meeting = LiveMeeting();
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.GetMessageByClientIdAsync(5, 1, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((MeetingMessage?)null);
        var uow = Substitute.For<IUnitOfWork>();
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new InvalidOperationException("connection reset"));
        var handler = new SendMeetingMessageCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, HostUser(),
            new MeetingTestPolicy(), uow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new SendMeetingMessageCommand(5, Guid.NewGuid(), "Lost"), CancellationToken.None));
    }

    // ---- Files -----------------------------------------------------------------------------------

    [Fact]
    public async Task UploadingAFile_IsRefusedOnceTheMeetingHoldsItsFileCount()
    {
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.CountAssetsAsync(5, Arg.Any<CancellationToken>()).Returns(4);
        var error = await Assert.ThrowsAsync<BusinessException>(() => UploadAsync(collaboration,
            new MeetingTestPolicy { MaxAssetsPerMeeting = 4 }));

        Assert.Equal("MEETING_FILE_COUNT_LIMIT_REACHED", error.Code);
        Assert.Contains("4", error.Message);
    }

    [Fact]
    public async Task UploadingAFile_IsRefusedWhenItWouldCrossTheMeetingStorageQuota()
    {
        var collaboration = Substitute.For<IMeetingCollaborationRepository>();
        collaboration.CountAssetsAsync(5, Arg.Any<CancellationToken>()).Returns(1);
        collaboration.GetAssetBytesAsync(5, Arg.Any<CancellationToken>()).Returns(2_000_000L);
        var error = await Assert.ThrowsAsync<BusinessException>(() => UploadAsync(collaboration,
            new MeetingTestPolicy { MaxStorageBytesPerMeeting = 2_000_010 }));

        Assert.Equal("MEETING_FILE_QUOTA_EXCEEDED", error.Code);
    }

    // ---- Recording -------------------------------------------------------------------------------

    [Fact]
    public async Task RequestingARecording_IsRefusedWhenDeploymentEgressCapacityIsInUse_BeforeAnyoneIsAsked()
    {
        var meeting = LiveMeeting();
        var recordings = Substitute.For<IMeetingRecordingRepository>();
        recordings.CountActiveAsync(Arg.Any<CancellationToken>()).Returns(1);
        var media = Substitute.For<IMeetingMediaProvider>(); media.IsEnabled.Returns(true);
        var handler = new RequestMeetingRecordingCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), recordings, HostUser(), media,
            new MeetingTestPolicy { MaxConcurrentRecordings = 1 }, Substitute.For<IUnitOfWork>(),
            Substitute.For<ILogger<RequestMeetingRecordingCommandHandler>>());

        var error = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
            new RequestMeetingRecordingCommand(5, 60), CancellationToken.None));

        Assert.Equal("MEETING_RECORDING_CAPACITY_REACHED", error.Code);
        // Asking a room to consent and then failing to start would leave people believing they were
        // recorded, so the refusal has to come before consent is requested from anyone.
        await recordings.DidNotReceive().AddAsync(Arg.Any<MeetingRecording>(), Arg.Any<CancellationToken>());
        await media.DidNotReceive().ListRoomParticipantIdentitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ---- Shared arrangement ----------------------------------------------------------------------

    private static async Task<MeetingAssetDto> UploadAsync(IMeetingCollaborationRepository collaboration,
        MeetingTestPolicy policy)
    {
        var meeting = LiveMeeting();
        var handler = new UploadMeetingAssetCommandHandler(MeetingsReturning(meeting),
            Substitute.For<IMeetingGuestAccessRepository>(), collaboration, HostUser(), policy,
            Substitute.For<IObjectStorage>(), Substitute.For<IPlannerAssetScanner>(), Substitute.For<IUnitOfWork>());
        var content = "safe meeting attachment"u8.ToArray();
        using var stream = new MemoryStream(content);
        return await handler.Handle(new UploadMeetingAssetCommand(5, "notes.txt", "text/plain",
            content.Length, stream, policy.MaxFileBytes), CancellationToken.None);
    }

    private static async Task<VerifiedMeetingGuestDto> VerifyGuest(Meeting meeting, IMeetingPolicy policy, string email)
    {
        var link = meeting.AccessLinks.FirstOrDefault();
        if (link is null)
        {
            link = meeting.AddAccessLink(new string('a', 64), MeetingAccessLinkMode.Reusable, null,
                MeetingAccessLevel.Participant, null, DateTime.UtcNow.AddDays(1), null);
            SetId(link, 9);
            // EF fills the back-reference from the aggregate; in memory it has to be set here or the
            // handler looks up meeting 0.
            typeof(MeetingAccessLink).GetProperty(nameof(MeetingAccessLink.MeetingId))!.SetValue(link, 5);
        }
        var normalized = email.ToUpperInvariant();
        var challenge = new MeetingGuestChallenge(5, 9, normalized, new string('c', 64),
            DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow.AddSeconds(-1), 5);
        var guestAccess = Substitute.For<IMeetingGuestAccessRepository>();
        guestAccess.GetLinkByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(link);
        guestAccess.GetLatestChallengeAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(challenge);
        var protector = Substitute.For<IMeetingGuestCodeProtector>();
        protector.Verify(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var handler = new VerifyMeetingGuestCodeCommandHandler(guestAccess, MeetingsReturning(meeting),
            protector, Substitute.For<IUserRepository>(), policy, Substitute.For<IUnitOfWork>());
        return await handler.Handle(new VerifyMeetingGuestCodeCommand(new string('t', 40), email,
            "123456", "Guest", false, null, null, 60), CancellationToken.None);
    }

    private static void FillRoster(Meeting meeting, int seats)
    {
        for (var index = 1; index < seats; index++)
            SetId(meeting.AddRegisteredParticipant(20 + index, MeetingAccessLevel.Participant), index + 1);
    }

    private static Meeting DraftMeeting()
    {
        var meeting = new Meeting(7, 11, "Review", null, null, null, "UTC", "meeting-room",
            true, true, true, true, true, false, 90);
        SetId(meeting, 5); SetId(meeting.Participants.Single(), 1);
        return meeting;
    }

    private static Meeting LiveMeeting()
    {
        var meeting = DraftMeeting(); meeting.Start(DateTime.UtcNow); return meeting;
    }

    private static ICurrentUserService HostUser()
    {
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(11); user.Email.Returns("host@example.test");
        return user;
    }

    private static IMeetingRepository MeetingsReturning(Meeting meeting)
    {
        var meetings = Substitute.For<IMeetingRepository>();
        meetings.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns(meeting);
        return meetings;
    }

    private static void SetId(object entity, int id) => entity.GetType().GetProperty("Id")!.SetValue(entity, id);
}
