using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Domain.Entities.Meetings;

public sealed class MeetingBadgeDefinition : AuditableEntity
{
    public int MeetingId { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string Color { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    private MeetingBadgeDefinition() { }
    internal MeetingBadgeDefinition(string label, string color, string? icon)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Trim().Length > 40) throw new ArgumentException("Badge label must be 1-40 characters.");
        if (label.IndexOfAny(['<', '>', '&']) >= 0 || label.Any(char.IsControl)) throw new ArgumentException("Badge label contains unsafe characters.");
        if (string.IsNullOrWhiteSpace(color) || !System.Text.RegularExpressions.Regex.IsMatch(color, "^[a-z][a-z0-9-]{0,23}$"))
            throw new ArgumentException("Badge color must be a safe palette key.");
        if (!string.IsNullOrWhiteSpace(icon) && !System.Text.RegularExpressions.Regex.IsMatch(icon, "^[A-Za-z][A-Za-z0-9-]{0,39}$"))
            throw new ArgumentException("Badge icon must be a safe icon key.");
        Label = label.Trim(); Color = color.Trim(); Icon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
    }
}

public sealed class MeetingParticipant : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int? UserId { get; private set; }
    public string? NormalizedEmail { get; private set; }
    public string? DisplayName { get; private set; }
    public MeetingAccessLevel AccessLevel { get; private set; }
    public int? BadgeDefinitionId { get; private set; }
    public MeetingParticipantState State { get; private set; }
    private MeetingParticipant() { }
    private MeetingParticipant(int userId, MeetingAccessLevel accessLevel, int? badgeDefinitionId, MeetingParticipantState state)
    { UserId = userId; AccessLevel = accessLevel; BadgeDefinitionId = badgeDefinitionId; State = state; }
    internal static MeetingParticipant RegisteredHost(int userId) => new(userId, MeetingAccessLevel.Host, null, MeetingParticipantState.Admitted);
    internal static MeetingParticipant Registered(int userId, MeetingAccessLevel accessLevel, int? badgeDefinitionId) =>
        new(userId, accessLevel, badgeDefinitionId, MeetingParticipantState.Invited);
    internal static MeetingParticipant Guest(string normalizedEmail, string displayName,
        MeetingAccessLevel accessLevel, int? badgeDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail)) throw new ArgumentException("Guest email is required.");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120)
            throw new ArgumentException("Guest display name must be 1-120 characters.");
        return new MeetingParticipant
        {
            NormalizedEmail = normalizedEmail.Trim().ToUpperInvariant(),
            DisplayName = displayName.Trim(), AccessLevel = accessLevel,
            BadgeDefinitionId = badgeDefinitionId, State = MeetingParticipantState.Invited
        };
    }
    internal void BindRegisteredUser(int userId)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        UserId = userId; MarkAsUpdated();
    }
    internal void ConfirmDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120)
            throw new ArgumentException("Display name must be 1-120 characters.");
        DisplayName = displayName.Trim(); MarkAsUpdated();
    }
    internal void Update(MeetingAccessLevel accessLevel, int? badgeDefinitionId, MeetingParticipantState state)
    { AccessLevel = accessLevel; BadgeDefinitionId = badgeDefinitionId; State = state; MarkAsUpdated(); }
}

public sealed class MeetingAccessLink : AuditableEntity
{
    public int MeetingId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public MeetingAccessLinkMode Mode { get; private set; }
    public string? LockedEmail { get; private set; }
    public MeetingAccessLevel DefaultAccessLevel { get; private set; }
    public int? BadgeDefinitionId { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public int? MaximumUses { get; private set; }
    public int UseCount { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    private MeetingAccessLink() { }
    internal MeetingAccessLink(string tokenHash, MeetingAccessLinkMode mode, string? lockedEmail,
        MeetingAccessLevel defaultAccessLevel, int? badgeDefinitionId, DateTime expiresAtUtc, int? maximumUses)
    {
        if (string.IsNullOrWhiteSpace(tokenHash)) throw new ArgumentException("Token hash is required.");
        if (expiresAtUtc <= DateTime.UtcNow) throw new ArgumentException("Access link expiry must be in the future.");
        if (maximumUses is <= 0) throw new ArgumentOutOfRangeException(nameof(maximumUses));
        TokenHash = tokenHash; Mode = mode;
        LockedEmail = string.IsNullOrWhiteSpace(lockedEmail) ? null : lockedEmail.Trim().ToUpperInvariant();
        DefaultAccessLevel = defaultAccessLevel; BadgeDefinitionId = badgeDefinitionId;
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc); MaximumUses = maximumUses;
    }
    internal void Revoke(DateTime utcNow)
    { if (!RevokedAtUtc.HasValue) { RevokedAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); MarkAsUpdated(); } }
    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;
    public bool HasCapacity => !MaximumUses.HasValue || UseCount < MaximumUses.Value;
    public bool IsAvailable(DateTime utcNow) => IsActive(utcNow) && HasCapacity;
    public void RegisterUse(DateTime utcNow)
    {
        if (!IsAvailable(utcNow)) throw new InvalidOperationException("Meeting access link is no longer available.");
        UseCount++; MarkAsUpdated();
    }
}

public sealed class MeetingGuestChallenge : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int AccessLinkId { get; private set; }
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime ResendAvailableAtUtc { get; private set; }
    public int FailedAttempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    private MeetingGuestChallenge() { }
    public MeetingGuestChallenge(int meetingId, int accessLinkId, string normalizedEmail, string codeHash,
        DateTime expiresAtUtc, DateTime resendAvailableAtUtc, int maxAttempts)
    {
        MeetingId = meetingId; AccessLinkId = accessLinkId;
        NormalizedEmail = normalizedEmail.Trim().ToUpperInvariant(); CodeHash = codeHash;
        ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        ResendAvailableAtUtc = DateTime.SpecifyKind(resendAvailableAtUtc, DateTimeKind.Utc);
        MaxAttempts = maxAttempts;
    }
    public bool CanAttempt(DateTime utcNow) => ConsumedAtUtc is null && utcNow < ExpiresAtUtc && FailedAttempts < MaxAttempts;
    public void Fail(DateTime utcNow) { if (!CanAttempt(utcNow)) return; FailedAttempts++; if (FailedAttempts >= MaxAttempts) ConsumedAtUtc = utcNow; MarkAsUpdated(); }
    public void Consume(DateTime utcNow) { ConsumedAtUtc ??= DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); MarkAsUpdated(); }
}

public sealed class MeetingGuestSession : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int ParticipantId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    private MeetingGuestSession() { }
    public MeetingGuestSession(int meetingId, int participantId, string tokenHash, DateTime expiresAtUtc)
    { MeetingId = meetingId; ParticipantId = participantId; TokenHash = tokenHash; ExpiresAtUtc = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc); }
    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && utcNow < ExpiresAtUtc;
    public void Revoke(DateTime utcNow) { RevokedAtUtc ??= DateTime.SpecifyKind(utcNow, DateTimeKind.Utc); MarkAsUpdated(); }
}

public sealed class MeetingGuestDecision : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int ParticipantId { get; private set; }
    public int ActorUserId { get; private set; }
    public MeetingGuestDecisionKind Kind { get; private set; }
    private MeetingGuestDecision() { }
    public MeetingGuestDecision(int meetingId, int participantId, int actorUserId, MeetingGuestDecisionKind kind)
    { MeetingId = meetingId; ParticipantId = participantId; ActorUserId = actorUserId; Kind = kind; }
}

public sealed class MeetingAttendance : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int ParticipantId { get; private set; }
    public string ProviderConnectionId { get; private set; } = string.Empty;
    public string? ProviderParticipantSid { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public DateTime? LeftAtUtc { get; private set; }
    private MeetingAttendance() { }
    internal MeetingAttendance(int participantId, string connectionId, string? participantSid, DateTime joinedAtUtc)
    { ParticipantId = participantId; ProviderConnectionId = connectionId; ProviderParticipantSid = participantSid; JoinedAtUtc = DateTime.SpecifyKind(joinedAtUtc, DateTimeKind.Utc); }
    internal void Close(DateTime leftAtUtc) { LeftAtUtc ??= DateTime.SpecifyKind(leftAtUtc, DateTimeKind.Utc); MarkAsUpdated(); }
}
