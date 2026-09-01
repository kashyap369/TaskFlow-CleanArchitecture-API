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
        // Organization members are explicitly assigned by an authorized organizer. Unlike an
        // email guest, they do not wait for an email-verification/admission workflow.
        new(userId, accessLevel, badgeDefinitionId, MeetingParticipantState.Admitted);
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
    internal void ReconcileJoin(string? participantSid, DateTime joinedAtUtc)
    {
        var occurred = DateTime.SpecifyKind(joinedAtUtc, DateTimeKind.Utc);
        if (occurred < JoinedAtUtc) JoinedAtUtc = occurred;
        if (!string.IsNullOrWhiteSpace(participantSid)) ProviderParticipantSid = participantSid;
        MarkAsUpdated();
    }
    internal void Close(DateTime leftAtUtc) { LeftAtUtc ??= DateTime.SpecifyKind(leftAtUtc, DateTimeKind.Utc); MarkAsUpdated(); }
}

public sealed class MeetingWebhookReceipt : AuditableEntity
{
    public int MeetingId { get; private set; }
    public string ProviderEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public DateTime OccurredAtUtc { get; private set; }

    private MeetingWebhookReceipt() { }
    public MeetingWebhookReceipt(int meetingId, string providerEventId, string eventType, DateTime occurredAtUtc)
    {
        if (meetingId <= 0) throw new ArgumentOutOfRangeException(nameof(meetingId));
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        MeetingId = meetingId;
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
    }
}

public sealed class MeetingMessage : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int AuthorParticipantId { get; private set; }
    public Guid ClientMessageId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public int? ReplyToMessageId { get; private set; }
    private MeetingMessage() { }
    public MeetingMessage(int meetingId, int authorParticipantId, Guid clientMessageId,
        string body, int? replyToMessageId = null)
    {
        if (meetingId <= 0) throw new ArgumentOutOfRangeException(nameof(meetingId));
        if (authorParticipantId <= 0) throw new ArgumentOutOfRangeException(nameof(authorParticipantId));
        if (clientMessageId == Guid.Empty) throw new ArgumentException("Client message id is required.");
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000)
            throw new ArgumentException("Message body must be 1-4000 characters.");
        MeetingId = meetingId; AuthorParticipantId = authorParticipantId;
        ClientMessageId = clientMessageId; Body = body.Trim(); ReplyToMessageId = replyToMessageId;
    }
}

public sealed class MeetingNote : AuditableEntity
{
    public int MeetingId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int Version { get; private set; }
    public int? LastEditedByParticipantId { get; private set; }
    private MeetingNote() { }
    public MeetingNote(int meetingId)
    { if (meetingId <= 0) throw new ArgumentOutOfRangeException(nameof(meetingId)); MeetingId = meetingId; Version = 0; }
    public void Update(string content, int expectedVersion, int editorParticipantId)
    {
        if (expectedVersion != Version) throw new InvalidOperationException("The meeting note has changed.");
        if (content.Length > 100_000) throw new ArgumentException("Meeting note cannot exceed 100,000 characters.");
        Content = content; Version++; LastEditedByParticipantId = editorParticipantId; MarkAsUpdated();
    }
}

public sealed class MeetingNoteRevision : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int NoteId { get; private set; }
    public int Version { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int EditorParticipantId { get; private set; }
    private MeetingNoteRevision() { }
    public MeetingNoteRevision(int meetingId, int noteId, int version, string content, int editorParticipantId)
    { MeetingId = meetingId; NoteId = noteId; Version = version; Content = content; EditorParticipantId = editorParticipantId; }
}

public sealed class MeetingAsset : AuditableEntity
{
    public int MeetingId { get; private set; }
    public int UploaderParticipantId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public MeetingAssetScanStatus ScanStatus { get; private set; }
    public DateTime RetainUntilUtc { get; private set; }
    private MeetingAsset() { }
    public MeetingAsset(int meetingId, int uploaderParticipantId, string storageKey, string fileName,
        string contentType, long sizeBytes, string sha256, DateTime retainUntilUtc)
    {
        if (meetingId <= 0 || uploaderParticipantId <= 0) throw new ArgumentOutOfRangeException();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey); ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType); ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        MeetingId = meetingId; UploaderParticipantId = uploaderParticipantId; StorageKey = storageKey;
        FileName = fileName; ContentType = contentType; SizeBytes = sizeBytes; Sha256 = sha256;
        RetainUntilUtc = DateTime.SpecifyKind(retainUntilUtc, DateTimeKind.Utc);
        ScanStatus = MeetingAssetScanStatus.Pending;
    }
    public void SetScanStatus(MeetingAssetScanStatus status) { ScanStatus = status; MarkAsUpdated(); }
}
