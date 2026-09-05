using System.Text.RegularExpressions;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Meetings;

public sealed record RemoveMeetingRoomParticipantCommand(int MeetingId, int ParticipantId) : IRequest;
public sealed record MuteMeetingRoomParticipantCommand(int MeetingId, int ParticipantId,
    string ParticipantIdentity, string TrackSid, bool Muted) : IRequest;
public sealed record RemoveGuestMeetingRoomParticipantCommand(string SessionToken, int ParticipantId) : IRequest;
public sealed record MuteGuestMeetingRoomParticipantCommand(string SessionToken, int ParticipantId,
    string ParticipantIdentity, string TrackSid, bool Muted) : IRequest;
public sealed record ProcessMeetingProviderWebhookCommand(MeetingProviderWebhook Webhook) : IRequest;

public sealed class MuteMeetingRoomParticipantCommandValidator : AbstractValidator<MuteMeetingRoomParticipantCommand>
{
    public MuteMeetingRoomParticipantCommandValidator()
    {
        RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.ParticipantId).GreaterThan(0);
        RuleFor(x => x.ParticipantIdentity).NotEmpty().MaximumLength(160);
        RuleFor(x => x.TrackSid).NotEmpty().MaximumLength(120);
    }
}

internal static partial class MeetingRoomModerationRules
{
    public static MeetingParticipant AuthenticatedModerator(Meeting meeting, int userId)
    {
        var actor = meeting.Participants.SingleOrDefault(x => x.UserId == userId && !x.IsDeleted)
            ?? throw new ForbiddenException("MEETING_MODERATION_DENIED", "You are not assigned to this meeting.");
        EnsureModerator(actor); return actor;
    }

    public static void EnsureModerator(MeetingParticipant actor)
    {
        if (actor.State != MeetingParticipantState.Admitted ||
            actor.AccessLevel is not (MeetingAccessLevel.Host or MeetingAccessLevel.CoHost))
            throw new ForbiddenException("MEETING_MODERATION_DENIED", "Host or co-host access is required.");
    }

    public static MeetingParticipant Target(Meeting meeting, MeetingParticipant actor, int targetId)
    {
        var target = meeting.Participants.SingleOrDefault(x => x.Id == targetId && !x.IsDeleted)
            ?? throw new NotFoundException("MEETING_PARTICIPANT_NOT_FOUND", "Meeting participant not found.");
        if (target.AccessLevel == MeetingAccessLevel.Host ||
            (actor.AccessLevel == MeetingAccessLevel.CoHost && target.AccessLevel == MeetingAccessLevel.CoHost))
            throw new ForbiddenException("MEETING_MODERATION_TARGET_DENIED", "You cannot moderate this participant.");
        return target;
    }

    public static void EnsureIdentity(Meeting meeting, MeetingParticipant target, string identity)
    {
        var prefix = IdentityPrefix(meeting.Id, target.Id);
        if (!identity.StartsWith(prefix, StringComparison.Ordinal) ||
            !ParticipantIdentityPattern().IsMatch(identity))
            throw new ForbiddenException("MEETING_PARTICIPANT_IDENTITY_INVALID", "The media participant identity is invalid.");
    }

    public static string IdentityPrefix(int meetingId, int participantId) => $"m{meetingId}-p{participantId}-";

    /// <summary>
    /// Reads the TaskFlow participant id back out of a media participant identity
    /// (<c>m{meetingId}-p{participantId}-{nonce}</c>). Identities are minted server-side, so this is
    /// the only supported way to map a provider roster entry onto a meeting participant.
    /// </summary>
    public static bool TryParticipantId(int meetingId, string identity, out int participantId)
    {
        participantId = 0;
        if (string.IsNullOrEmpty(identity)) return false;
        var prefix = $"m{meetingId}-p";
        if (!identity.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var separator = identity.IndexOf('-', prefix.Length);
        return separator > prefix.Length && int.TryParse(identity[prefix.Length..separator], out participantId)
            && participantId > 0;
    }

    [GeneratedRegex("^m[1-9][0-9]*-p[1-9][0-9]*-[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex ParticipantIdentityPattern();
}

internal sealed class MeetingRoomModeratorAccess(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess)
{
    public async Task<(Meeting Meeting, MeetingParticipant Actor)> ForUserAsync(int meetingId,
        int userId, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(meetingId, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        MeetingRoomAccessRules.EnsureMeetingLive(meeting);
        return (meeting, MeetingRoomModerationRules.AuthenticatedModerator(meeting, userId));
    }

    public async Task<(Meeting Meeting, MeetingParticipant Actor)> ForGuestAsync(string sessionToken,
        CancellationToken ct)
    {
        var session = await guestAccess.GetSessionByHashAsync(MeetingGuestAccessRules.Hash(sessionToken), ct);
        if (session is null || !session.IsActive(DateTime.UtcNow))
            throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session has expired.");
        var meeting = await meetings.GetByIdAsync(session.MeetingId, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        MeetingRoomAccessRules.EnsureMeetingLive(meeting);
        var actor = meeting.Participants.SingleOrDefault(x => x.Id == session.ParticipantId && !x.IsDeleted)
            ?? throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting access is no longer available.");
        MeetingRoomModerationRules.EnsureModerator(actor);
        return (meeting, actor);
    }
}

public sealed class RemoveMeetingRoomParticipantCommandHandler(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess, ICurrentUserService user, IMeetingMediaProvider media,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveMeetingRoomParticipantCommand>
{
    public async Task Handle(RemoveMeetingRoomParticipantCommand request, CancellationToken ct)
    {
        var (meeting, actor) = await new MeetingRoomModeratorAccess(meetings, guestAccess)
            .ForUserAsync(request.MeetingId, user.UserId, ct);
        await RemoveAsync(meeting, actor, request.ParticipantId, meetings, guestAccess, media, unitOfWork, ct);
    }

    internal static async Task RemoveAsync(Meeting meeting, MeetingParticipant actor, int targetId,
        IMeetingRepository meetings, IMeetingGuestAccessRepository guestAccess,
        IMeetingMediaProvider media, IUnitOfWork unitOfWork, CancellationToken ct)
    {
        var target = MeetingRoomModerationRules.Target(meeting, actor, targetId);
        meeting.UpdateParticipant(target.Id, target.AccessLevel, target.BadgeDefinitionId,
            MeetingParticipantState.Removed);
        foreach (var session in await guestAccess.GetActiveSessionsAsync(target.Id, ct))
        { session.Revoke(DateTime.UtcNow); guestAccess.UpdateSession(session); }
        meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct);
        await media.RemoveParticipantsAsync(meeting.RoomName,
            MeetingRoomModerationRules.IdentityPrefix(meeting.Id, target.Id), ct);
    }
}

public sealed class RemoveGuestMeetingRoomParticipantCommandHandler(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess, IMeetingMediaProvider media, IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveGuestMeetingRoomParticipantCommand>
{
    public async Task Handle(RemoveGuestMeetingRoomParticipantCommand request, CancellationToken ct)
    {
        var (meeting, actor) = await new MeetingRoomModeratorAccess(meetings, guestAccess)
            .ForGuestAsync(request.SessionToken, ct);
        await RemoveMeetingRoomParticipantCommandHandler.RemoveAsync(meeting, actor,
            request.ParticipantId, meetings, guestAccess, media, unitOfWork, ct);
    }
}

public sealed class MuteMeetingRoomParticipantCommandHandler(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess, ICurrentUserService user, IMeetingMediaProvider media)
    : IRequestHandler<MuteMeetingRoomParticipantCommand>
{
    public async Task Handle(MuteMeetingRoomParticipantCommand request, CancellationToken ct)
    {
        var (meeting, actor) = await new MeetingRoomModeratorAccess(meetings, guestAccess)
            .ForUserAsync(request.MeetingId, user.UserId, ct);
        await MuteAsync(meeting, actor, request.ParticipantId, request.ParticipantIdentity,
            request.TrackSid, request.Muted, media, ct);
    }

    internal static async Task MuteAsync(Meeting meeting, MeetingParticipant actor, int targetId,
        string identity, string trackSid, bool muted, IMeetingMediaProvider media, CancellationToken ct)
    {
        var target = MeetingRoomModerationRules.Target(meeting, actor, targetId);
        MeetingRoomModerationRules.EnsureIdentity(meeting, target, identity);
        await media.MuteTrackAsync(meeting.RoomName, identity, trackSid, muted, ct);
    }
}

public sealed class MuteGuestMeetingRoomParticipantCommandHandler(IMeetingRepository meetings,
    IMeetingGuestAccessRepository guestAccess, IMeetingMediaProvider media)
    : IRequestHandler<MuteGuestMeetingRoomParticipantCommand>
{
    public async Task Handle(MuteGuestMeetingRoomParticipantCommand request, CancellationToken ct)
    {
        var (meeting, actor) = await new MeetingRoomModeratorAccess(meetings, guestAccess)
            .ForGuestAsync(request.SessionToken, ct);
        await MuteMeetingRoomParticipantCommandHandler.MuteAsync(meeting, actor, request.ParticipantId,
            request.ParticipantIdentity, request.TrackSid, request.Muted, media, ct);
    }
}

public sealed class ProcessMeetingProviderWebhookCommandHandler(IMeetingRepository meetings,
    IMeetingRecordingRepository recordings, IUnitOfWork unitOfWork, IMeetingPolicy policy)
    : IRequestHandler<ProcessMeetingProviderWebhookCommand>
{
    public async Task Handle(ProcessMeetingProviderWebhookCommand request, CancellationToken ct)
    {
        var webhook = request.Webhook;
        if (string.IsNullOrWhiteSpace(webhook.EventId) || webhook.EventId.Length > 120 ||
            string.IsNullOrWhiteSpace(webhook.RoomName)) return;
        if (await meetings.HasWebhookReceiptAsync(webhook.EventId, ct)) return;
        var meeting = await meetings.GetByRoomNameAsync(webhook.RoomName, ct);
        if (meeting is null) return;
        var occurred = webhook.OccurredAtUtc?.UtcDateTime ?? DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(webhook.EgressId))
        {
            var recording = await recordings.GetByProviderEgressIdAsync(webhook.EgressId, ct);
            if (recording is null) return;
            var status = webhook.EgressStatus ?? string.Empty;
            if (status.Contains("Active", StringComparison.OrdinalIgnoreCase)) recording.MarkRecording(occurred);
            else if (status.Contains("Complete", StringComparison.OrdinalIgnoreCase)) recording.MarkReady(occurred, webhook.EgressFileSize, webhook.EgressDurationMilliseconds);
            else if (status.Contains("Ending", StringComparison.OrdinalIgnoreCase)) recording.MarkProcessing(occurred);
            else if (status.Contains("Failed", StringComparison.OrdinalIgnoreCase) || status.Contains("Aborted", StringComparison.OrdinalIgnoreCase) || status.Contains("Limit", StringComparison.OrdinalIgnoreCase)) recording.Fail(webhook.EgressError ?? "The recording provider reported a failure.");
            else return;
            await meetings.AddWebhookReceiptAsync(new MeetingWebhookReceipt(meeting.Id, webhook.EventId, webhook.EventType, occurred), ct);
            recordings.Update(recording); await unitOfWork.SaveChangesAsync(ct); return;
        }

        if (string.Equals(webhook.EventType, "room_finished", StringComparison.Ordinal))
        {
            // The room empties whenever the last participant leaves, including after a client fault
            // that ejected everyone seconds in. Ending on that would archive a meeting nobody
            // attended, so require one genuine session before treating this as the meeting's end.
            var attended = meeting.HasSubstantiveAttendance(occurred,
                policy.AutoEndMinimumSessionSeconds);
            meeting.EndFromProvider(occurred, attended);
        }
        else if (!string.IsNullOrWhiteSpace(webhook.ParticipantIdentity) &&
                 MeetingRoomModerationRules.TryParticipantId(meeting.Id, webhook.ParticipantIdentity, out var participantId))
        {
            if (string.Equals(webhook.EventType, "participant_joined", StringComparison.Ordinal))
                meeting.RegisterParticipantJoined(participantId, webhook.ParticipantIdentity,
                    webhook.ParticipantSid, occurred);
            else if (string.Equals(webhook.EventType, "participant_left", StringComparison.Ordinal))
                meeting.RegisterParticipantLeft(participantId, webhook.ParticipantIdentity,
                    webhook.ParticipantSid, occurred);
            else return;
        }
        else return;

        await meetings.AddWebhookReceiptAsync(new MeetingWebhookReceipt(meeting.Id, webhook.EventId,
            webhook.EventType, occurred), ct);
        meetings.Update(meeting);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
