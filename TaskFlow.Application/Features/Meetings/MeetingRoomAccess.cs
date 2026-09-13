using MediatR;
using System.Diagnostics;
using System.Text.Json;
using TaskFlow.Application.Common.Observability;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;

namespace TaskFlow.Application.Features.Meetings;

public sealed record GetMeetingJoinTokenCommand(int MeetingId) : IRequest<MeetingRoomTokenDto>;
public sealed record GetGuestMeetingJoinTokenCommand(string SessionToken) : IRequest<MeetingRoomTokenDto>;

// The browser receives only an opaque participant identity, the LiveKit URL, and a short-lived token.
// It never decides capabilities or sees a provider secret/room identifier.
public sealed record MeetingRoomTokenDto(string WebSocketUrl, string Token, DateTimeOffset ExpiresAtUtc,
    int MeetingId, int ParticipantId, string DisplayName, MeetingAccessLevel AccessLevel,
    string? BadgeLabel, bool CanPublish, bool CanShareScreen, bool CanModerate,
    string ParticipantIdentity, string MeetingTitle);

internal static class MeetingRoomAccessRules
{
    /// <summary>
    /// Phase 7 / P7.4. Counts every join attempt and its outcome around the whole issuing path, not
    /// just the token call, because a join fails far more often before the provider is reached —
    /// meeting not live, access revoked, guest not admitted, consent outstanding. The refusal
    /// <i>code</i> is the tag; it is a fixed vocabulary, so it cannot inflate cardinality, and it is
    /// the one thing an operator needs to tell "the media stack is broken" apart from "the host
    /// revoked someone". No identity, room name or token is recorded.
    /// </summary>
    public static async Task<MeetingRoomTokenDto> ObserveAsync(string actor,
        Func<Task<MeetingRoomTokenDto>> issue)
    {
        try
        {
            var token = await issue();
            MeetingTelemetry.JoinTokens.Add(1, new TagList
            {
                { MeetingTelemetry.Tags.Actor, actor },
                { MeetingTelemetry.Tags.Outcome, MeetingTelemetry.Outcomes.Issued }
            });
            return token;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MeetingTelemetry.JoinTokens.Add(1, new TagList
            {
                { MeetingTelemetry.Tags.Actor, actor },
                { MeetingTelemetry.Tags.Outcome, MeetingTelemetry.Outcomes.Refused },
                { MeetingTelemetry.Tags.Reason, (exception as BusinessException)?.Code ?? "unhandled" }
            });
            throw;
        }
    }

    public static void EnsureMeetingLive(Meeting meeting)
    {
        if (meeting.Status != MeetingStatus.Live)
            throw new BusinessException("MEETING_ROOM_NOT_LIVE", "The meeting room is not live.");
    }

    public static void EnsureParticipantAllowed(MeetingParticipant participant)
    {
        if (participant.State is MeetingParticipantState.Revoked or MeetingParticipantState.Denied or MeetingParticipantState.Removed)
            throw new UnauthorizedException("MEETING_ROOM_ACCESS_REVOKED", "Your meeting access has been removed.");
    }

    public static async Task<MeetingRoomTokenDto> CreateAsync(Meeting meeting, MeetingParticipant participant,
        string displayName, IMeetingMediaProvider provider, CancellationToken ct)
    {
        EnsureMeetingLive(meeting); EnsureParticipantAllowed(participant);
        await ReleaseEarlierSessionsAsync(meeting, participant, provider, ct);
        var canPublish = participant.AccessLevel != MeetingAccessLevel.Viewer && meeting.ParticipantsCanPublish;
        var canShareScreen = canPublish && meeting.ParticipantsCanShareScreen;
        var canModerate = participant.AccessLevel is MeetingAccessLevel.Host or MeetingAccessLevel.CoHost;
        var badge = meeting.Badges.FirstOrDefault(x => x.Id == participant.BadgeDefinitionId)?.Label;
        var identity = $"m{meeting.Id}-p{participant.Id}-{Guid.NewGuid():N}";
        var metadata = JsonSerializer.Serialize(new
        {
            participantId = participant.Id,
            accessLevel = participant.AccessLevel,
            badgeLabel = badge
        });
        var issued = provider.CreateJoinToken(new MeetingJoinTokenRequest(
            meeting.RoomName, identity, displayName,
            TimeSpan.FromMinutes(10), canPublish, true, true, canModerate, metadata));
        return new(provider.WebSocketUrl, issued.Value, issued.ExpiresAtUtc, meeting.Id, participant.Id,
            displayName, participant.AccessLevel, badge, canPublish, canShareScreen, canModerate,
            identity, meeting.Title);
    }

    /// <summary>
    /// Ends this participant's earlier media sessions before a new identity is handed out.
    ///
    /// Identities carry a per-join nonce (<c>m{meetingId}-p{participantId}-{nonce}</c>), which is
    /// what lets moderation sweep every session a participant holds by prefix. The cost is that
    /// LiveKit's own DUPLICATE_IDENTITY protection can never fire, because two sessions of the same
    /// person never share an identity: a refresh, a second tab, or a session stranded by a crash
    /// left that participant publishing the same microphone twice, and everyone else in the room
    /// heard them twice, slightly offset. Rejoining is authoritative now — the newest join wins and
    /// earlier sessions are ejected, which is the protection the nonce had otherwise given up.
    ///
    /// A failure here never blocks the join. This is cleanup, and refusing someone their seat in a
    /// meeting because a stale session could not be cleared would trade a self-healing annoyance for
    /// one the caller cannot recover from. The provider already measures both calls it makes here,
    /// so a persistently failing sweep stays visible in the media-call metrics without raising here.
    /// </summary>
    private static async Task ReleaseEarlierSessionsAsync(Meeting meeting, MeetingParticipant participant,
        IMeetingMediaProvider provider, CancellationToken ct)
    {
        try
        {
            await provider.RemoveParticipantsAsync(meeting.RoomName,
                MeetingRoomModerationRules.IdentityPrefix(meeting.Id, participant.Id), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A room that does not exist yet, or a provider that is briefly unreachable, must not
            // cost this participant their seat.
        }
    }
}

public sealed class GetMeetingJoinTokenCommandHandler(IMeetingRepository meetings,
    ICurrentUserService user, IMeetingMediaProvider provider, IMeetingRecordingRepository recordings)
    : IRequestHandler<GetMeetingJoinTokenCommand, MeetingRoomTokenDto>
{
    public Task<MeetingRoomTokenDto> Handle(GetMeetingJoinTokenCommand request, CancellationToken ct) =>
        MeetingRoomAccessRules.ObserveAsync(MeetingTelemetry.Actors.Member, () => IssueAsync(request, ct));

    private async Task<MeetingRoomTokenDto> IssueAsync(GetMeetingJoinTokenCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var participant = meeting.Participants.SingleOrDefault(x => x.UserId == user.UserId && !x.IsDeleted);
        if (participant is null)
            throw new ForbiddenException("MEETING_ROOM_ACCESS_DENIED", "You are not assigned to this meeting.");
        var displayName = participant.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = user.Email.Split('@')[0];
        await EnsureRecordingConsentAsync(meeting.Id, participant.Id, recordings, ct);
        return await MeetingRoomAccessRules.CreateAsync(meeting, participant, displayName, provider, ct);
    }

    internal static async Task EnsureRecordingConsentAsync(int meetingId, int participantId,
        IMeetingRecordingRepository recordings, CancellationToken ct)
    {
        var active = await recordings.GetActiveAsync(meetingId, ct);
        if (active is not null && active.Status is MeetingRecordingStatus.PendingConsent or MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording &&
            !active.HasAcceptedConsent(participantId))
            throw new BusinessException("MEETING_RECORDING_CONSENT_REQUIRED", "Accept the recording disclosure before joining this meeting.");
    }
}

public sealed class GetGuestMeetingJoinTokenCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IMeetingMediaProvider provider, IMeetingRecordingRepository recordings)
    : IRequestHandler<GetGuestMeetingJoinTokenCommand, MeetingRoomTokenDto>
{
    public Task<MeetingRoomTokenDto> Handle(GetGuestMeetingJoinTokenCommand request, CancellationToken ct) =>
        MeetingRoomAccessRules.ObserveAsync(MeetingTelemetry.Actors.Guest, () => IssueAsync(request, ct));

    private async Task<MeetingRoomTokenDto> IssueAsync(GetGuestMeetingJoinTokenCommand request, CancellationToken ct)
    {
        var session = await guestAccess.GetSessionByHashAsync(MeetingGuestAccessRules.Hash(request.SessionToken), ct);
        if (session is null || !session.IsActive(DateTime.UtcNow))
            throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session has expired. Verify your email again.");
        var meeting = await meetings.GetByIdAsync(session.MeetingId, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var participant = meeting.Participants.SingleOrDefault(x => x.Id == session.ParticipantId && !x.IsDeleted)
            ?? throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting access is no longer available.");
        if (participant.State != MeetingParticipantState.Admitted)
            throw new ForbiddenException("MEETING_GUEST_NOT_ADMITTED", "Wait for the host to admit you before joining.");
        await GetMeetingJoinTokenCommandHandler.EnsureRecordingConsentAsync(meeting.Id, participant.Id, recordings, ct);
        return await MeetingRoomAccessRules.CreateAsync(meeting, participant,
            participant.DisplayName ?? "Guest", provider, ct);
    }
}
