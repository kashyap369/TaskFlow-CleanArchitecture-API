using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using TaskFlow.Application.Contracts.Email;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Application.Exceptions;
using TaskFlow.Domain.Entities.Meetings;
using TaskFlow.Domain.Enums.Meetings;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Domain.Interfaces.Meetings;
using TaskFlow.Domain.Interfaces.Persistence;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Application.Features.Meetings;

public sealed record InspectMeetingGuestAccessCommand(string Token) : IRequest<MeetingGuestAccessDto>;
public sealed record RequestMeetingGuestCodeCommand(string Token, string Email) : IRequest;
public sealed record VerifyMeetingGuestCodeCommand(string Token, string Email, string Code, string DisplayName,
    bool BindRegisteredAccount, int? AuthenticatedUserId, string? AuthenticatedEmail, int SessionMinutes) : IRequest<VerifiedMeetingGuestDto>;
public sealed record GetMeetingGuestSessionQuery(string SessionToken) : IRequest<MeetingGuestSessionDto>;
public sealed record ConfirmMeetingGuestDisplayNameCommand(string SessionToken, string DisplayName) : IRequest<MeetingGuestSessionDto>;

public sealed record MeetingGuestAccessDto(string Title, string? Description, DateTime? ScheduledStartUtc,
    string TimeZone, string HostName, MeetingAccessLinkMode Mode, string? LockedEmailHint,
    MeetingAccessLevel AccessLevel, string? BadgeLabel, bool LoggedInEmailMatches);
public sealed record VerifiedMeetingGuestDto(string SessionToken, DateTime ExpiresAtUtc, MeetingGuestSessionDto Session);
public sealed record MeetingGuestSessionDto(int MeetingId, int ParticipantId, string Title, string? Description,
    DateTime? ScheduledStartUtc, string TimeZone, string HostName, string DisplayName, string Email,
    MeetingAccessLevel AccessLevel, string? BadgeLabel, MeetingParticipantState State, DateTime ExpiresAtUtc);

public sealed class InspectMeetingGuestAccessValidator : AbstractValidator<InspectMeetingGuestAccessCommand>
{ public InspectMeetingGuestAccessValidator() => RuleFor(x => x.Token).NotEmpty().MaximumLength(100); }
public sealed class RequestMeetingGuestCodeValidator : AbstractValidator<RequestMeetingGuestCodeCommand>
{ public RequestMeetingGuestCodeValidator() { RuleFor(x => x.Token).NotEmpty().MaximumLength(100); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320); } }
public sealed class VerifyMeetingGuestCodeValidator : AbstractValidator<VerifyMeetingGuestCodeCommand>
{ public VerifyMeetingGuestCodeValidator() { RuleFor(x => x.Token).NotEmpty().MaximumLength(100); RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320); RuleFor(x => x.Code).Matches("^[0-9]{6}$"); RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120); RuleFor(x => x.SessionMinutes).InclusiveBetween(5, 1440); } }
public sealed class ConfirmMeetingGuestDisplayNameValidator : AbstractValidator<ConfirmMeetingGuestDisplayNameCommand>
{ public ConfirmMeetingGuestDisplayNameValidator() { RuleFor(x => x.SessionToken).NotEmpty().MaximumLength(100); RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120); } }

internal static class MeetingGuestAccessRules
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Normalize(string email) => new Email(email).Value.ToUpperInvariant();
    public static void EnsureAvailable(Meeting meeting, MeetingAccessLink link, string? normalizedEmail = null)
    {
        if (!meeting.GuestsAllowed) throw new BusinessException("MEETING_GUESTS_DISABLED", "Guest access is disabled for this meeting.");
        if (meeting.Status == MeetingStatus.Cancelled) throw new BusinessException("MEETING_NOT_JOINABLE", "This meeting is no longer accepting guests.");
        if (!link.IsActive(DateTime.UtcNow)) throw new BusinessException("MEETING_LINK_UNAVAILABLE", "This invitation is expired or revoked.");
        if (normalizedEmail is not null && !link.HasCapacity && !meeting.Participants.Any(x => x.NormalizedEmail == normalizedEmail && !x.IsDeleted))
            throw new BusinessException("MEETING_LINK_UNAVAILABLE", "This invitation has reached its use limit.");
        if (normalizedEmail is not null && link.Mode == MeetingAccessLinkMode.PrivateInvitation && link.LockedEmail != normalizedEmail)
            throw new BusinessException("MEETING_ACCESS_INVALID", "This invitation cannot be used with that email address.");
    }
    public static string Mask(string email)
    {
        var parts = email.Split('@'); if (parts.Length != 2) return "••••";
        return $"{parts[0][0]}{new string('•', Math.Min(6, Math.Max(2, parts[0].Length - 1)))}@{parts[1]}".ToLowerInvariant();
    }
    /// <summary>
    /// A guest session only proves that this browser verified an email once. Every later use must
    /// re-check the participant, because the organizer can revoke, deny or remove them at any time —
    /// and the participant row can be gone entirely.
    /// </summary>
    public static MeetingParticipant EnsureStillAllowed(Meeting meeting, int participantId)
    {
        var participant = meeting.Participants.SingleOrDefault(x => x.Id == participantId && !x.IsDeleted)
            ?? throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting access is no longer available.");
        if (participant.State is MeetingParticipantState.Revoked or MeetingParticipantState.Denied or MeetingParticipantState.Removed)
            throw new UnauthorizedException("MEETING_GUEST_ACCESS_REVOKED", "The host has ended this guest's access.");
        return participant;
    }
    public static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class InspectMeetingGuestAccessCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IUserRepository users) : IRequestHandler<InspectMeetingGuestAccessCommand, MeetingGuestAccessDto>
{
    public async Task<MeetingGuestAccessDto> Handle(InspectMeetingGuestAccessCommand request, CancellationToken ct)
    {
        var link = await guestAccess.GetLinkByHashAsync(MeetingGuestAccessRules.Hash(request.Token), ct)
            ?? throw new BusinessException("MEETING_ACCESS_INVALID", "This meeting invitation is invalid or no longer available.");
        var meeting = await meetings.GetByIdAsync(link.MeetingId, ct) ?? throw new BusinessException("MEETING_ACCESS_INVALID", "This meeting invitation is invalid or no longer available.");
        MeetingGuestAccessRules.EnsureAvailable(meeting, link);
        var host = await users.GetByIdAsync(meeting.CreatedByUserId, ct);
        return new(meeting.Title, meeting.Description, meeting.ScheduledStartUtc, meeting.TimeZone,
            host?.FullName.DisplayName ?? "TaskFlow host", link.Mode,
            link.LockedEmail is null ? null : MeetingGuestAccessRules.Mask(link.LockedEmail), link.DefaultAccessLevel,
            meeting.Badges.FirstOrDefault(x => x.Id == link.BadgeDefinitionId)?.Label, false);
    }
}

public sealed class RequestMeetingGuestCodeCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IMeetingGuestCodeProtector protector, IEmailService emailService,
    IUnitOfWork unitOfWork) : IRequestHandler<RequestMeetingGuestCodeCommand>
{
    public async Task Handle(RequestMeetingGuestCodeCommand request, CancellationToken ct)
    {
        var link = await guestAccess.GetLinkByHashAsync(MeetingGuestAccessRules.Hash(request.Token), ct)
            ?? throw new BusinessException("MEETING_ACCESS_INVALID", "If the invitation and email are valid, a code can be requested.");
        var meeting = await meetings.GetByIdAsync(link.MeetingId, ct) ?? throw new BusinessException("MEETING_ACCESS_INVALID", "If the invitation and email are valid, a code can be requested.");
        var normalized = MeetingGuestAccessRules.Normalize(request.Email);
        MeetingGuestAccessRules.EnsureAvailable(meeting, link, normalized);
        var latest = await guestAccess.GetLatestChallengeAsync(link.Id, normalized, ct);
        if (latest is not null && latest.ConsumedAtUtc is null && latest.ResendAvailableAtUtc > DateTime.UtcNow) return;
        if (latest is not null && latest.ConsumedAtUtc is null) { latest.Consume(DateTime.UtcNow); guestAccess.UpdateChallenge(latest); }
        var code = protector.GenerateCode(); var now = DateTime.UtcNow;
        await guestAccess.AddChallengeAsync(new MeetingGuestChallenge(meeting.Id, link.Id, normalized,
            protector.Protect(link.Id, normalized, code), now.AddMinutes(10), now.AddSeconds(60), 5), ct);
        await unitOfWork.SaveChangesAsync(ct);
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Email", "Templates", "MeetingGuestCode.html");
        var template = await File.ReadAllTextAsync(templatePath, ct);
        template = template.Replace("{{MeetingTitle}}", WebUtility.HtmlEncode(meeting.Title)).Replace("{{Code}}", code)
            .Replace("{{CurrentYear}}", DateTime.UtcNow.Year.ToString());
        await emailService.SendAsync(request.Email.Trim(), $"Your code for {meeting.Title}", template, ct);
    }
}

public sealed class VerifyMeetingGuestCodeCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IMeetingGuestCodeProtector protector, IUserRepository users,
    IMeetingPolicy policy, IUnitOfWork unitOfWork) : IRequestHandler<VerifyMeetingGuestCodeCommand, VerifiedMeetingGuestDto>
{
    public async Task<VerifiedMeetingGuestDto> Handle(VerifyMeetingGuestCodeCommand request, CancellationToken ct)
    {
        var link = await guestAccess.GetLinkByHashAsync(MeetingGuestAccessRules.Hash(request.Token), ct)
            ?? throw InvalidCode();
        var meeting = await meetings.GetByIdAsync(link.MeetingId, ct) ?? throw InvalidCode();
        var normalized = MeetingGuestAccessRules.Normalize(request.Email); MeetingGuestAccessRules.EnsureAvailable(meeting, link, normalized);
        var challenge = await guestAccess.GetLatestChallengeAsync(link.Id, normalized, ct);
        if (challenge is null || !challenge.CanAttempt(DateTime.UtcNow) || !protector.Verify(link.Id, normalized, request.Code, challenge.CodeHash))
        {
            if (challenge?.CanAttempt(DateTime.UtcNow) == true) { challenge.Fail(DateTime.UtcNow); guestAccess.UpdateChallenge(challenge); await unitOfWork.SaveChangesAsync(ct); }
            throw InvalidCode();
        }
        if (request.BindRegisteredAccount && request.AuthenticatedUserId.HasValue &&
            (string.IsNullOrWhiteSpace(request.AuthenticatedEmail) || MeetingGuestAccessRules.Normalize(request.AuthenticatedEmail) != normalized))
            throw new ConflictException("MEETING_ACCOUNT_EMAIL_MISMATCH", "The signed-in account uses a different email. Sign out or use the invitation email without binding the account.");
        var existingParticipant = meeting.Participants.FirstOrDefault(x => x.NormalizedEmail == normalized && !x.IsDeleted);
        if (existingParticipant?.State is MeetingParticipantState.Revoked or MeetingParticipantState.Denied or MeetingParticipantState.Removed)
            throw new UnauthorizedException("MEETING_GUEST_ACCESS_REVOKED", "The host has ended this guest's access.");
        // A reusable link is the one path that can add participants without an organizer acting, so
        // the seat ceiling has to hold here too — otherwise a shared link fills a room past the
        // capacity TaskFlow declares. An email that already holds a seat is re-admitted, not counted
        // twice, so a guest reconnecting from a new browser is never refused for capacity.
        if (existingParticipant is null) MeetingCapacityRules.EnsureParticipantSeat(meeting, policy);
        var participant = meeting.AddGuestParticipant(normalized, request.DisplayName, link.DefaultAccessLevel, link.BadgeDefinitionId);
        if (request.BindRegisteredAccount)
        {
            if (!request.AuthenticatedUserId.HasValue) throw new ConflictException("MEETING_ACCOUNT_REQUIRED", "Sign in to bind this invitation to a TaskFlow account.");
            meeting.BindGuestParticipant(participant.Id, request.AuthenticatedUserId.Value);
        }
        challenge.Consume(DateTime.UtcNow); guestAccess.UpdateChallenge(challenge); if (existingParticipant is null) link.RegisterUse(DateTime.UtcNow);
        meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct);
        var rawSession = MeetingGuestAccessRules.RandomToken(); var expires = DateTime.UtcNow.AddMinutes(request.SessionMinutes);
        await guestAccess.AddSessionAsync(new MeetingGuestSession(meeting.Id, participant.Id,
            MeetingGuestAccessRules.Hash(rawSession), expires, link.Id), ct);
        await unitOfWork.SaveChangesAsync(ct);
        var host = await users.GetByIdAsync(meeting.CreatedByUserId, ct);
        var dto = ToSession(meeting, participant, request.Email.Trim(), host?.FullName.DisplayName ?? "TaskFlow host", expires);
        return new(rawSession, expires, dto);
    }
    private static BusinessException InvalidCode() => new("MEETING_CODE_INVALID", "The code is invalid or expired. Request a new code and try again.");
    internal static MeetingGuestSessionDto ToSession(Meeting meeting, MeetingParticipant participant, string email, string host, DateTime expires) =>
        new(meeting.Id, participant.Id, meeting.Title, meeting.Description, meeting.ScheduledStartUtc, meeting.TimeZone,
            host, participant.DisplayName ?? email, email, participant.AccessLevel,
            meeting.Badges.FirstOrDefault(x => x.Id == participant.BadgeDefinitionId)?.Label, participant.State, expires);
}

public sealed class GetMeetingGuestSessionQueryHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IUserRepository users) : IRequestHandler<GetMeetingGuestSessionQuery, MeetingGuestSessionDto>
{
    public async Task<MeetingGuestSessionDto> Handle(GetMeetingGuestSessionQuery request, CancellationToken ct)
    {
        var session = await guestAccess.GetSessionByHashAsync(MeetingGuestAccessRules.Hash(request.SessionToken), ct);
        if (session is null || !session.IsActive(DateTime.UtcNow)) throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session has expired. Verify your email again.");
        var meeting = await meetings.GetByIdAsync(session.MeetingId, ct) ?? throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session is no longer available.");
        var participant = MeetingGuestAccessRules.EnsureStillAllowed(meeting, session.ParticipantId);
        var host = await users.GetByIdAsync(meeting.CreatedByUserId, ct);
        return VerifyMeetingGuestCodeCommandHandler.ToSession(meeting, participant, participant.NormalizedEmail ?? string.Empty,
            host?.FullName.DisplayName ?? "TaskFlow host", session.ExpiresAtUtc);
    }
}

public sealed class ConfirmMeetingGuestDisplayNameCommandHandler(IMeetingGuestAccessRepository guestAccess,
    IMeetingRepository meetings, IUserRepository users, IUnitOfWork unitOfWork) : IRequestHandler<ConfirmMeetingGuestDisplayNameCommand, MeetingGuestSessionDto>
{
    public async Task<MeetingGuestSessionDto> Handle(ConfirmMeetingGuestDisplayNameCommand request, CancellationToken ct)
    {
        var session = await guestAccess.GetSessionByHashAsync(MeetingGuestAccessRules.Hash(request.SessionToken), ct);
        if (session is null || !session.IsActive(DateTime.UtcNow)) throw new UnauthorizedException("MEETING_GUEST_SESSION_INVALID", "Your meeting session has expired.");
        var meeting = await meetings.GetByIdAsync(session.MeetingId, ct) ?? throw new NotFoundException("MEETING_NOT_FOUND", "Meeting not found.");
        MeetingGuestAccessRules.EnsureStillAllowed(meeting, session.ParticipantId);
        meeting.ConfirmGuestDisplayName(session.ParticipantId, request.DisplayName); meetings.Update(meeting); await unitOfWork.SaveChangesAsync(ct);
        var participant = meeting.Participants.Single(x => x.Id == session.ParticipantId); var host = await users.GetByIdAsync(meeting.CreatedByUserId, ct);
        return VerifyMeetingGuestCodeCommandHandler.ToSession(meeting, participant, participant.NormalizedEmail ?? string.Empty,
            host?.FullName.DisplayName ?? "TaskFlow host", session.ExpiresAtUtc);
    }
}
