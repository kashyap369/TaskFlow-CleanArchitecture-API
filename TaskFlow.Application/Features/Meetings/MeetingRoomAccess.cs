using MediatR;
using System.Text.Json;
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

    public static MeetingRoomTokenDto Create(Meeting meeting, MeetingParticipant participant,
        string displayName, IMeetingMediaProvider provider)
    {
        EnsureMeetingLive(meeting); EnsureParticipantAllowed(participant);
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
}

public sealed class GetMeetingJoinTokenCommandHandler(IMeetingRepository meetings,
    ICurrentUserService user, IMeetingMediaProvider provider)
    : IRequestHandler<GetMeetingJoinTokenCommand, MeetingRoomTokenDto>
{
    public async Task<MeetingRoomTokenDto> Handle(GetMeetingJoinTokenCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.MeetingId, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        var participant = meeting.Participants.SingleOrDefault(x => x.UserId == user.UserId && !x.IsDeleted);
        if (participant is null)
            throw new ForbiddenException("MEETING_ROOM_ACCESS_DENIED", "You are not assigned to this meeting.");
        var displayName = participant.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = user.Email.Split('@')[0];
        return MeetingRoomAccessRules.Create(meeting, participant, displayName, provider);
    }
}

public sealed class GetGuestMeetingJoinTokenCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IMeetingMediaProvider provider)
    : IRequestHandler<GetGuestMeetingJoinTokenCommand, MeetingRoomTokenDto>
{
    public async Task<MeetingRoomTokenDto> Handle(GetGuestMeetingJoinTokenCommand request, CancellationToken ct)
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
        return MeetingRoomAccessRules.Create(meeting, participant, participant.DisplayName ?? "Guest", provider);
    }
}
