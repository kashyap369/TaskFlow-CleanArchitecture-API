using System.Security.Cryptography;
using System.Net;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Configuration;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Domain.Interfaces.Persistence;

namespace TaskFlow.Application.Features.Meetings;

public sealed record CreateMeetingCommand(int OrganizationId, string Title, string? Description,
    DateTimeOffset? ScheduledStartUtc, DateTimeOffset? ScheduledEndUtc, string TimeZone = "UTC",
    bool LobbyEnabled = true, bool GuestsAllowed = false, bool ParticipantsCanPublish = true,
    bool ParticipantsCanShareScreen = true, bool ParticipantsCanEditNote = true,
    bool ViewersCanChat = false, int RetentionDays = 90,
    IReadOnlyList<MeetingBadgeInput>? Badges = null, IReadOnlyList<int>? ParticipantUserIds = null) : IRequest<int>;

public sealed record UpdateMeetingCommand(int Id, string Title, string? Description,
    DateTimeOffset? ScheduledStartUtc, DateTimeOffset? ScheduledEndUtc, string TimeZone = "UTC",
    bool LobbyEnabled = true, bool GuestsAllowed = false, bool ParticipantsCanPublish = true,
    bool ParticipantsCanShareScreen = true, bool ParticipantsCanEditNote = true,
    bool ViewersCanChat = false, int RetentionDays = 90) : IRequest;
public sealed record StartMeetingCommand(int Id) : IRequest;
public sealed record EndMeetingCommand(int Id) : IRequest;
public sealed record CancelMeetingCommand(int Id) : IRequest;
public sealed record AddMeetingBadgeCommand(int MeetingId, string Label, string Color, string? Icon) : IRequest<int>;
public sealed record AddMeetingParticipantCommand(int MeetingId, int UserId,
    MeetingAccessLevel AccessLevel = MeetingAccessLevel.Participant, int? BadgeDefinitionId = null) : IRequest<int>;
public sealed record UpdateMeetingParticipantCommand(int MeetingId, int ParticipantId,
    MeetingAccessLevel AccessLevel, int? BadgeDefinitionId, MeetingParticipantState State) : IRequest;
public sealed record CreateMeetingAccessLinkCommand(int MeetingId, MeetingAccessLinkMode Mode,
    string? LockedEmail, MeetingAccessLevel DefaultAccessLevel, int? BadgeDefinitionId,
    DateTimeOffset ExpiresAtUtc, int? MaximumUses) : IRequest<CreatedMeetingAccessLinkDto>;
public sealed record RevokeMeetingAccessLinkCommand(int MeetingId, int LinkId) : IRequest;
public sealed record RotateMeetingAccessLinkCommand(int MeetingId, int LinkId) : IRequest<CreatedMeetingAccessLinkDto>;

internal static class MeetingValidationRules
{
    public static void Apply<T>(AbstractValidator<T> validator, Func<T, string> title,
        Func<T, DateTimeOffset?> start, Func<T, DateTimeOffset?> end, Func<T, string> timeZone,
        Func<T, int> retention)
    {
        validator.RuleFor(x => title(x)).NotEmpty().MaximumLength(160);
        validator.RuleFor(x => x).Must(x => start(x).HasValue == end(x).HasValue)
            .WithMessage("A schedule requires both start and end.");
        validator.RuleFor(x => x).Must(x => !start(x).HasValue || end(x) > start(x))
            .WithMessage("Scheduled end must be after start.");
        validator.RuleFor(x => timeZone(x)).NotEmpty().MaximumLength(100).Must(BeTimeZone)
            .WithMessage("Time zone is not recognized.");
        validator.RuleFor(x => retention(x)).InclusiveBetween(1, 3650);
    }
    private static bool BeTimeZone(string value)
    { try { _ = TimeZoneInfo.FindSystemTimeZoneById(value); return true; } catch { return false; } }
}

public sealed class CreateMeetingCommandValidator : AbstractValidator<CreateMeetingCommand>
{
    public CreateMeetingCommandValidator()
    {
        RuleFor(x => x.OrganizationId).GreaterThan(0);
        MeetingValidationRules.Apply(this, x => x.Title, x => x.ScheduledStartUtc,
            x => x.ScheduledEndUtc, x => x.TimeZone, x => x.RetentionDays);
        RuleFor(x => x.Badges).Must(x => x is null || x.Count <= 20).WithMessage("A meeting can define at most 20 badges.");
        RuleFor(x => x.ParticipantUserIds).Must(x => x is null || x.Distinct().Count() == x.Count)
            .WithMessage("Participant user ids must be unique.");
    }
}
public sealed class UpdateMeetingCommandValidator : AbstractValidator<UpdateMeetingCommand>
{
    public UpdateMeetingCommandValidator()
    { RuleFor(x => x.Id).GreaterThan(0); MeetingValidationRules.Apply(this, x => x.Title,
        x => x.ScheduledStartUtc, x => x.ScheduledEndUtc, x => x.TimeZone, x => x.RetentionDays); }
}
public sealed class AddMeetingBadgeCommandValidator : AbstractValidator<AddMeetingBadgeCommand>
{
    public AddMeetingBadgeCommandValidator()
    { RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.Label).NotEmpty().MaximumLength(40);
      RuleFor(x => x.Label).Must(x => x.IndexOfAny(['<', '>', '&']) < 0 && !x.Any(char.IsControl))
          .WithMessage("Badge label contains unsafe characters.");
      RuleFor(x => x.Color).NotEmpty().Matches("^[a-z][a-z0-9-]{0,23}$");
      RuleFor(x => x.Icon).Matches("^[A-Za-z][A-Za-z0-9-]{0,39}$").When(x => !string.IsNullOrWhiteSpace(x.Icon)); }
}
public sealed class AddMeetingParticipantCommandValidator : AbstractValidator<AddMeetingParticipantCommand>
{
    public AddMeetingParticipantCommandValidator()
    { RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.UserId).GreaterThan(0);
      RuleFor(x => x.AccessLevel).IsInEnum().NotEqual(MeetingAccessLevel.Host); }
}
public sealed class UpdateMeetingParticipantCommandValidator : AbstractValidator<UpdateMeetingParticipantCommand>
{
    public UpdateMeetingParticipantCommandValidator()
    { RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.ParticipantId).GreaterThan(0);
      RuleFor(x => x.AccessLevel).IsInEnum(); RuleFor(x => x.State).IsInEnum(); }
}
public sealed class CreateMeetingAccessLinkCommandValidator : AbstractValidator<CreateMeetingAccessLinkCommand>
{
    public CreateMeetingAccessLinkCommandValidator()
    {
        RuleFor(x => x.MeetingId).GreaterThan(0); RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.DefaultAccessLevel).IsInEnum().NotEqual(MeetingAccessLevel.Host);
        RuleFor(x => x.ExpiresAtUtc).GreaterThan(DateTimeOffset.UtcNow);
        RuleFor(x => x.MaximumUses).GreaterThan(0).When(x => x.MaximumUses.HasValue);
        RuleFor(x => x.LockedEmail).NotEmpty().EmailAddress()
            .When(x => x.Mode == MeetingAccessLinkMode.PrivateInvitation);
    }
}

internal sealed class MeetingCommandAccess(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions)
{
    public async Task<Meeting> LoadManageableAsync(int id, CancellationToken cancellationToken)
    {
        var meeting = await meetings.GetByIdAsync(id, cancellationToken) ??
            throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        if (meeting.CreatedByUserId != user.UserId)
            await permissions.EnsurePermissionAsync(meeting.OrganizationId, user.UserId,
                OrganizationPermissionNames.ManageMeetings, cancellationToken);
        return meeting;
    }
}

public sealed class CreateMeetingCommandHandler(IMeetingRepository meetings,
    IOrganizationMemberRepository members, IOrganizationPermissionChecker permissions,
    ICurrentUserService user, IUnitOfWork unitOfWork) : IRequestHandler<CreateMeetingCommand, int>
{
    public async Task<int> Handle(CreateMeetingCommand request, CancellationToken cancellationToken)
    {
        await permissions.EnsurePermissionAsync(request.OrganizationId, user.UserId,
            OrganizationPermissionNames.CreateMeetings, cancellationToken);
        var meeting = new Meeting(request.OrganizationId, user.UserId, request.Title, request.Description,
            request.ScheduledStartUtc?.UtcDateTime, request.ScheduledEndUtc?.UtcDateTime, request.TimeZone,
            $"meeting-{Guid.NewGuid():N}", request.LobbyEnabled, request.GuestsAllowed,
            request.ParticipantsCanPublish, request.ParticipantsCanShareScreen,
            request.ParticipantsCanEditNote, request.ViewersCanChat, request.RetentionDays);
        foreach (var badge in request.Badges ?? [])
            Execute(() => meeting.AddBadge(badge.Label, badge.Color, badge.Icon));
        foreach (var participantUserId in request.ParticipantUserIds ?? [])
        {
            if (!await members.IsActiveMemberAsync(request.OrganizationId, participantUserId, cancellationToken))
                throw new NotFoundException("MEETING_PARTICIPANT_NOT_FOUND", "An active participant was not found in this organization.");
            Execute(() => meeting.AddRegisteredParticipant(participantUserId, MeetingAccessLevel.Participant));
        }
        await meetings.AddAsync(meeting, cancellationToken); await unitOfWork.SaveChangesAsync(cancellationToken);
        return meeting.Id;
    }
    private static void Execute(Action action)
    { try { action(); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
      { throw new BusinessException("MEETING_RULE_VIOLATION", ex.Message); } }
}

public sealed class UpdateMeetingCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork) : IRequestHandler<UpdateMeetingCommand>
{
    public async Task Handle(UpdateMeetingCommand request, CancellationToken cancellationToken)
    {
        var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.Id, cancellationToken);
        Execute(() => meeting.Update(request.Title, request.Description, request.ScheduledStartUtc?.UtcDateTime,
            request.ScheduledEndUtc?.UtcDateTime, request.TimeZone, request.LobbyEnabled,
            request.GuestsAllowed, request.ParticipantsCanPublish, request.ParticipantsCanShareScreen,
            request.ParticipantsCanEditNote, request.ViewersCanChat, request.RetentionDays));
        meetings.Update(meeting); await unitOfWork.SaveChangesAsync(cancellationToken);
    }
    internal static void Execute(Action action)
    { try { action(); } catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
      { throw new BusinessException("MEETING_RULE_VIOLATION", ex.Message); } }
}

public abstract class MeetingLifecycleHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork)
{
    protected async Task Mutate(int id, Action<Meeting> mutation, CancellationToken cancellationToken)
    {
        var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(id, cancellationToken);
        UpdateMeetingCommandHandler.Execute(() => mutation(meeting)); meetings.Update(meeting);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
public sealed class StartMeetingCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork)
    : MeetingLifecycleHandler(meetings, user, permissions, unitOfWork), IRequestHandler<StartMeetingCommand>
{ public Task Handle(StartMeetingCommand request, CancellationToken ct) => Mutate(request.Id, x => x.Start(DateTime.UtcNow), ct); }
public sealed class EndMeetingCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork, IMeetingMediaProvider mediaProvider,
    IMeetingRecordingRepository recordings, ILogger<EndMeetingCommandHandler> logger)
    : MeetingLifecycleHandler(meetings, user, permissions, unitOfWork), IRequestHandler<EndMeetingCommand>
{
    public async Task Handle(EndMeetingCommand request, CancellationToken ct)
    {
        var meeting = await meetings.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        await Mutate(request.Id, x => { x.End(DateTime.UtcNow); x.CloseOpenAttendance(DateTime.UtcNow); }, ct);
        var activeRecording = await recordings.GetActiveAsync(request.Id, ct);
        if (activeRecording is not null && activeRecording.Status is MeetingRecordingStatus.Starting or MeetingRecordingStatus.Recording &&
            !string.IsNullOrWhiteSpace(activeRecording.ProviderEgressId))
        {
            try
            {
                await mediaProvider.StopRoomRecordingAsync(activeRecording.ProviderEgressId, ct);
                activeRecording.MarkProcessing(DateTime.UtcNow); recordings.Update(activeRecording);
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger.LogError(exception, "Could not stop recording for meeting {MeetingId}", meeting.Id); }
        }
        if (mediaProvider.IsEnabled)
        {
            try { await mediaProvider.CloseRoomAsync(meeting.RoomName, ct); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The TaskFlow lifecycle remains authoritative. LiveKit rooms also close after their
                // configured empty timeout; webhook reconciliation finalizes any open attendance.
                logger.LogError(exception, "Could not close media room for meeting {MeetingId}", meeting.Id);
            }
        }
    }
}
public sealed class CancelMeetingCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork)
    : MeetingLifecycleHandler(meetings, user, permissions, unitOfWork), IRequestHandler<CancelMeetingCommand>
{ public Task Handle(CancelMeetingCommand request, CancellationToken ct) => Mutate(request.Id, x => x.Cancel(), ct); }

public sealed class AddMeetingBadgeCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork) : IRequestHandler<AddMeetingBadgeCommand, int>
{
    public async Task<int> Handle(AddMeetingBadgeCommand request, CancellationToken ct)
    { var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
      MeetingBadgeDefinition? badge = null; UpdateMeetingCommandHandler.Execute(() => badge = meeting.AddBadge(request.Label, request.Color, request.Icon));
      meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct); return badge!.Id; }
}

public sealed class AddMeetingParticipantCommandHandler(IMeetingRepository meetings,
    IOrganizationMemberRepository members, ICurrentUserService user, IOrganizationPermissionChecker permissions,
    IUnitOfWork unitOfWork) : IRequestHandler<AddMeetingParticipantCommand, int>
{
    public async Task<int> Handle(AddMeetingParticipantCommand request, CancellationToken ct)
    { var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
      if (!await members.IsActiveMemberAsync(meeting.OrganizationId, request.UserId, ct))
          throw new NotFoundException("MEETING_PARTICIPANT_NOT_FOUND", "The selected active organization member was not found.");
      MeetingParticipant? participant = null; UpdateMeetingCommandHandler.Execute(() => participant = meeting.AddRegisteredParticipant(request.UserId, request.AccessLevel, request.BadgeDefinitionId));
      meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct); return participant!.Id; }
}

public sealed class UpdateMeetingParticipantCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IMeetingGuestAccessRepository guestAccess,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateMeetingParticipantCommand>
{
    public async Task Handle(UpdateMeetingParticipantCommand request, CancellationToken ct)
    { var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
      var participant = meeting.Participants.SingleOrDefault(x => x.Id == request.ParticipantId);
      UpdateMeetingCommandHandler.Execute(() => meeting.UpdateParticipant(request.ParticipantId, request.AccessLevel, request.BadgeDefinitionId, request.State));
      if (participant?.NormalizedEmail is not null && request.State is MeetingParticipantState.Admitted or MeetingParticipantState.Denied or MeetingParticipantState.Revoked or MeetingParticipantState.Removed)
      {
          var kind = request.State switch { MeetingParticipantState.Admitted => MeetingGuestDecisionKind.Admitted,
              MeetingParticipantState.Denied => MeetingGuestDecisionKind.Denied,
              MeetingParticipantState.Removed => MeetingGuestDecisionKind.Removed, _ => MeetingGuestDecisionKind.Revoked };
          await guestAccess.AddDecisionAsync(new MeetingGuestDecision(meeting.Id, request.ParticipantId, user.UserId, kind), ct);
          if (request.State is not MeetingParticipantState.Admitted)
              foreach (var session in await guestAccess.GetActiveSessionsAsync(request.ParticipantId, ct)) { session.Revoke(DateTime.UtcNow); guestAccess.UpdateSession(session); }
      }
      meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct); }
}

public sealed class CreateMeetingAccessLinkCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IUnitOfWork unitOfWork, IEmailService emailService,
    IClientUrlProvider clientUrl) : IRequestHandler<CreateMeetingAccessLinkCommand, CreatedMeetingAccessLinkDto>
{
    public async Task<CreatedMeetingAccessLinkDto> Handle(CreateMeetingAccessLinkCommand request, CancellationToken ct)
    {
        var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
        if (!meeting.GuestsAllowed) throw new BusinessException("MEETING_GUESTS_DISABLED", "Enable guests in the meeting agreement before creating guest access.");
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
        MeetingAccessLink? link = null;
        UpdateMeetingCommandHandler.Execute(() => link = meeting.AddAccessLink(hash, request.Mode,
            request.LockedEmail, request.DefaultAccessLevel, request.BadgeDefinitionId,
            request.ExpiresAtUtc.UtcDateTime, request.MaximumUses));
        meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct);
        if (request.Mode == MeetingAccessLinkMode.PrivateInvitation && !string.IsNullOrWhiteSpace(request.LockedEmail))
        {
            var template = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Email", "Templates", "MeetingInvitation.html"), ct);
            var joinUrl = $"{clientUrl.BaseUrl}/meetings/join#token={Uri.EscapeDataString(token)}";
            template = template.Replace("{{MeetingTitle}}", WebUtility.HtmlEncode(meeting.Title))
                .Replace("{{HostName}}", WebUtility.HtmlEncode(user.Email))
                .Replace("{{Email}}", WebUtility.HtmlEncode(request.LockedEmail.Trim()))
                .Replace("{{JoinUrl}}", WebUtility.HtmlEncode(joinUrl))
                .Replace("{{Expiry}}", link!.ExpiresAtUtc.ToString("f"))
                .Replace("{{CurrentYear}}", DateTime.UtcNow.Year.ToString());
            await emailService.SendAsync(request.LockedEmail.Trim(), $"Invitation: {meeting.Title}", template, ct);
        }
        return new(link!.Id, token, link.ExpiresAtUtc);
    }
}

/// <summary>
/// Revoking a link is the organizer's only lever when a link leaks, so it has to reach the people who
/// already used it. Marking the link revoked alone stops new verifications while everyone holding a
/// session keeps the room, chat, files and archive until it expires by itself. Revoking those sessions
/// closes that window: the guest is ejected from the media room and cannot verify again, because the
/// link they would verify against is gone.
/// </summary>
internal static class MeetingAccessLinkRevocation
{
    public static async Task RevokeIssuedSessionsAsync(Meeting meeting, int linkId,
        IMeetingGuestAccessRepository guestAccess, IMeetingMediaProvider media,
        ILogger logger, CancellationToken ct)
    {
        var sessions = await guestAccess.GetActiveSessionsForLinkAsync(linkId, ct);
        if (sessions.Count == 0) return;
        var now = DateTime.UtcNow;
        foreach (var session in sessions) { session.Revoke(now); guestAccess.UpdateSession(session); }
        if (meeting.Status != MeetingStatus.Live || !media.IsEnabled) return;
        foreach (var participantId in sessions.Select(x => x.ParticipantId).Distinct())
        {
            try
            {
                await media.RemoveParticipantsAsync(meeting.RoomName,
                    MeetingRoomModerationRules.IdentityPrefix(meeting.Id, participantId), ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The session is already dead in TaskFlow, so the guest cannot rejoin or reach any
                // meeting data. Their current media connection outlives this only until the room ends.
                logger.LogError(exception,
                    "Could not eject participant {ParticipantId} from meeting {MeetingId} after link revocation",
                    participantId, meeting.Id);
            }
        }
    }
}

public sealed class RevokeMeetingAccessLinkCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IMeetingGuestAccessRepository guestAccess,
    IMeetingMediaProvider media, ILogger<RevokeMeetingAccessLinkCommandHandler> logger,
    IUnitOfWork unitOfWork) : IRequestHandler<RevokeMeetingAccessLinkCommand>
{
    public async Task Handle(RevokeMeetingAccessLinkCommand request, CancellationToken ct)
    { var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
      UpdateMeetingCommandHandler.Execute(() => meeting.RevokeAccessLink(request.LinkId, DateTime.UtcNow));
      await MeetingAccessLinkRevocation.RevokeIssuedSessionsAsync(meeting, request.LinkId, guestAccess, media, logger, ct);
      meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct); }
}

public sealed class RotateMeetingAccessLinkCommandHandler(IMeetingRepository meetings, ICurrentUserService user,
    IOrganizationPermissionChecker permissions, IMeetingGuestAccessRepository guestAccess,
    IMeetingMediaProvider media, ILogger<RotateMeetingAccessLinkCommandHandler> logger,
    IUnitOfWork unitOfWork) : IRequestHandler<RotateMeetingAccessLinkCommand, CreatedMeetingAccessLinkDto>
{
    public async Task<CreatedMeetingAccessLinkDto> Handle(RotateMeetingAccessLinkCommand request, CancellationToken ct)
    {
        var meeting = await new MeetingCommandAccess(meetings, user, permissions).LoadManageableAsync(request.MeetingId, ct);
        var old = meeting.AccessLinks.SingleOrDefault(x => x.Id == request.LinkId && !x.IsDeleted)
            ?? throw new NotFoundException("MEETING_ACCESS_LINK_NOT_FOUND", "Meeting access link not found.");
        meeting.RevokeAccessLink(old.Id, DateTime.UtcNow);
        await MeetingAccessLinkRevocation.RevokeIssuedSessionsAsync(meeting, old.Id, guestAccess, media, logger, ct);
        var token = MeetingGuestAccessRules.RandomToken();
        // A rotation is meant to hand out a working replacement. Reusing an expiry that has already
        // passed would mint a link that is dead on arrival, so keep the later of the two.
        var expires = old.ExpiresAtUtc > DateTime.UtcNow ? old.ExpiresAtUtc : DateTime.UtcNow.AddDays(7);
        var replacement = meeting.AddAccessLink(MeetingGuestAccessRules.Hash(token), old.Mode, old.LockedEmail,
            old.DefaultAccessLevel, old.BadgeDefinitionId, expires, old.MaximumUses);
        meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct);
        return new(replacement.Id, token, replacement.ExpiresAtUtc);
    }
}
