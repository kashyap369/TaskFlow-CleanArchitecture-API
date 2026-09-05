using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Domain.Entities.Meetings;

public sealed class Meeting : AuditableEntity, IAggregateRoot
{
    private readonly List<MeetingBadgeDefinition> _badges = [];
    private readonly List<MeetingParticipant> _participants = [];
    private readonly List<MeetingAccessLink> _accessLinks = [];
    private readonly List<MeetingAttendance> _attendance = [];

    public int OrganizationId { get; private set; }
    public int CreatedByUserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime? ScheduledStartUtc { get; private set; }
    public DateTime? ScheduledEndUtc { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public MeetingStatus Status { get; private set; }
    public DateTime? ActualStartUtc { get; private set; }
    public DateTime? ActualEndUtc { get; private set; }
    public string RoomName { get; private set; } = string.Empty;
    public bool LobbyEnabled { get; private set; }
    public bool GuestsAllowed { get; private set; }
    public bool ParticipantsCanPublish { get; private set; }
    public bool ParticipantsCanShareScreen { get; private set; }
    public bool ParticipantsCanEditNote { get; private set; }
    public bool ViewersCanChat { get; private set; }
    public int RetentionDays { get; private set; }
    public IReadOnlyCollection<MeetingBadgeDefinition> Badges => _badges.AsReadOnly();
    public IReadOnlyCollection<MeetingParticipant> Participants => _participants.AsReadOnly();
    public IReadOnlyCollection<MeetingAccessLink> AccessLinks => _accessLinks.AsReadOnly();
    public IReadOnlyCollection<MeetingAttendance> Attendance => _attendance.AsReadOnly();

    private Meeting() { }

    public Meeting(int organizationId, int createdByUserId, string title, string? description,
        DateTime? scheduledStartUtc, DateTime? scheduledEndUtc, string timeZone, string roomName,
        bool lobbyEnabled, bool guestsAllowed, bool participantsCanPublish,
        bool participantsCanShareScreen, bool participantsCanEditNote, bool viewersCanChat,
        int retentionDays)
    {
        if (organizationId <= 0) throw new ArgumentOutOfRangeException(nameof(organizationId));
        if (createdByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(createdByUserId));
        if (string.IsNullOrWhiteSpace(roomName)) throw new ArgumentException("Room name is required.", nameof(roomName));
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        RoomName = roomName.Trim();
        Apply(title, description, scheduledStartUtc, scheduledEndUtc, timeZone, lobbyEnabled,
            guestsAllowed, participantsCanPublish, participantsCanShareScreen,
            participantsCanEditNote, viewersCanChat, retentionDays);
        Status = scheduledStartUtc.HasValue ? MeetingStatus.Scheduled : MeetingStatus.Draft;
        _participants.Add(MeetingParticipant.RegisteredHost(createdByUserId));
    }

    public void Update(string title, string? description, DateTime? scheduledStartUtc,
        DateTime? scheduledEndUtc, string timeZone, bool lobbyEnabled, bool guestsAllowed,
        bool participantsCanPublish, bool participantsCanShareScreen,
        bool participantsCanEditNote, bool viewersCanChat, int retentionDays)
    {
        if (Status is MeetingStatus.Live or MeetingStatus.Ended or MeetingStatus.Cancelled)
            throw new InvalidOperationException("Only draft or scheduled meetings can be edited.");
        Apply(title, description, scheduledStartUtc, scheduledEndUtc, timeZone, lobbyEnabled,
            guestsAllowed, participantsCanPublish, participantsCanShareScreen,
            participantsCanEditNote, viewersCanChat, retentionDays);
        Status = scheduledStartUtc.HasValue ? MeetingStatus.Scheduled : MeetingStatus.Draft;
        MarkAsUpdated();
    }

    public void Start(DateTime utcNow)
    {
        if (Status is not (MeetingStatus.Draft or MeetingStatus.Scheduled))
            throw new InvalidOperationException("Only draft or scheduled meetings can start.");
        Status = MeetingStatus.Live;
        ActualStartUtc = AsUtc(utcNow);
        MarkAsUpdated();
    }

    public void End(DateTime utcNow)
    {
        if (Status != MeetingStatus.Live)
            throw new InvalidOperationException("Only a live meeting can end.");
        Status = MeetingStatus.Ended;
        ActualEndUtc = AsUtc(utcNow);
        MarkAsUpdated();
    }

    /// <summary>
    /// The media provider reported that the room closed. The room empties whenever the last
    /// participant leaves — including when everyone was dropped by a client fault — so ending the
    /// meeting here is only correct when the call actually happened. Otherwise a failed join would
    /// permanently archive a meeting nobody attended, which is what it did in production on
    /// 2026-09-04. Attendance is always closed; <paramref name="endMeeting"/> decides the rest.
    /// </summary>
    public void EndFromProvider(DateTime utcNow, bool endMeeting = true)
    {
        if (endMeeting && Status == MeetingStatus.Live)
        {
            Status = MeetingStatus.Ended;
            ActualEndUtc = AsUtc(utcNow);
            MarkAsUpdated();
        }
        CloseOpenAttendance(utcNow);
    }

    /// <summary>
    /// Whether the room ever carried a real session: at least one attendance interval lasting
    /// <paramref name="minimumSeconds"/> or more. A three-second connect-and-drop is not a meeting.
    /// </summary>
    public bool HasSubstantiveAttendance(DateTime utcNow, int minimumSeconds)
    {
        return _attendance.Any(interval =>
            ((interval.LeftAtUtc ?? AsUtc(utcNow)) - interval.JoinedAtUtc).TotalSeconds >= minimumSeconds);
    }

    public void RegisterParticipantJoined(int participantId, string connectionId,
        string? participantSid, DateTime joinedAtUtc)
    {
        if (!_participants.Any(x => x.Id == participantId && !x.IsDeleted)) return;
        var existing = _attendance.SingleOrDefault(x => x.ProviderConnectionId == connectionId);
        if (existing is not null)
        {
            existing.ReconcileJoin(participantSid, joinedAtUtc);
            return;
        }
        _attendance.Add(new MeetingAttendance(participantId, connectionId, participantSid, joinedAtUtc));
        MarkAsUpdated();
    }

    public void RegisterParticipantLeft(int participantId, string connectionId,
        string? participantSid, DateTime leftAtUtc)
    {
        if (!_participants.Any(x => x.Id == participantId && !x.IsDeleted)) return;
        var existing = _attendance.SingleOrDefault(x => x.ProviderConnectionId == connectionId);
        if (existing is null)
        {
            existing = new MeetingAttendance(participantId, connectionId, participantSid, leftAtUtc);
            _attendance.Add(existing);
        }
        existing.Close(leftAtUtc);
        MarkAsUpdated();
    }

    public void CloseOpenAttendance(DateTime leftAtUtc)
    {
        foreach (var interval in _attendance.Where(x => x.LeftAtUtc is null)) interval.Close(leftAtUtc);
        MarkAsUpdated();
    }

    public void Cancel()
    {
        if (Status is not (MeetingStatus.Draft or MeetingStatus.Scheduled))
            throw new InvalidOperationException("Only draft or scheduled meetings can be cancelled.");
        Status = MeetingStatus.Cancelled;
        MarkAsUpdated();
    }

    public MeetingBadgeDefinition AddBadge(string label, string color, string? icon)
    {
        if (_badges.Any(x => !x.IsDeleted && string.Equals(x.Label, label.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A badge with this label already exists.");
        var badge = new MeetingBadgeDefinition(label, color, icon);
        _badges.Add(badge);
        MarkAsUpdated();
        return badge;
    }

    /// <summary>
    /// Everyone still holding a place on this meeting: the people who can be issued a join token.
    /// Removed, revoked and denied rows are excluded — they cannot come back without a new decision,
    /// so they must not consume a seat forever.
    /// </summary>
    public int ActiveParticipantCount => _participants.Count(x => !x.IsDeleted &&
        x.State is not (MeetingParticipantState.Removed or MeetingParticipantState.Revoked or MeetingParticipantState.Denied));

    /// <summary>
    /// The roster is the only gate on room size: a join token is issued to assigned participants
    /// only, so the seat ceiling is what keeps a meeting inside the capacity TaskFlow declares.
    /// The limit is configuration, so it is supplied rather than known here.
    /// </summary>
    public void EnsureParticipantCapacity(int maxParticipants)
    {
        if (ActiveParticipantCount >= maxParticipants)
            throw new InvalidOperationException(
                $"This meeting has reached its limit of {maxParticipants} participants.");
    }

    public MeetingParticipant AddRegisteredParticipant(int userId, MeetingAccessLevel accessLevel,
        int? badgeDefinitionId = null)
    {
        if (userId == CreatedByUserId || _participants.Any(x => !x.IsDeleted && x.UserId == userId))
            throw new InvalidOperationException("This user is already assigned to the meeting.");
        if (accessLevel == MeetingAccessLevel.Host)
            throw new InvalidOperationException("The meeting creator is the only host.");
        var participant = MeetingParticipant.Registered(userId, accessLevel, badgeDefinitionId);
        _participants.Add(participant);
        MarkAsUpdated();
        return participant;
    }

    public MeetingParticipant AddGuestParticipant(string normalizedEmail, string displayName,
        MeetingAccessLevel accessLevel, int? badgeDefinitionId)
    {
        var existing = _participants.FirstOrDefault(x => !x.IsDeleted && x.NormalizedEmail == normalizedEmail.Trim().ToUpperInvariant());
        if (existing is not null) return existing;
        if (accessLevel == MeetingAccessLevel.Host) throw new InvalidOperationException("Guests cannot be meeting hosts.");
        if (badgeDefinitionId.HasValue && !_badges.Any(x => x.Id == badgeDefinitionId && !x.IsDeleted))
            throw new InvalidOperationException("Meeting badge not found.");
        var participant = MeetingParticipant.Guest(normalizedEmail, displayName, accessLevel, badgeDefinitionId);
        _participants.Add(participant); MarkAsUpdated(); return participant;
    }

    public void BindGuestParticipant(int participantId, int userId)
    {
        var participant = _participants.Single(x => x.Id == participantId && !x.IsDeleted);
        if (_participants.Any(x => x.Id != participantId && !x.IsDeleted && x.UserId == userId))
            throw new InvalidOperationException("This account is already assigned to the meeting.");
        participant.BindRegisteredUser(userId); MarkAsUpdated();
    }

    public void ConfirmGuestDisplayName(int participantId, string displayName)
    {
        var participant = _participants.Single(x => x.Id == participantId && !x.IsDeleted);
        participant.ConfirmDisplayName(displayName); MarkAsUpdated();
    }

    public void UpdateParticipant(int participantId, MeetingAccessLevel accessLevel,
        int? badgeDefinitionId, MeetingParticipantState state)
    {
        var participant = _participants.SingleOrDefault(x => x.Id == participantId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Meeting participant not found.");
        if (participant.UserId == CreatedByUserId)
        {
            if (accessLevel != MeetingAccessLevel.Host || state == MeetingParticipantState.Revoked)
                throw new InvalidOperationException("The meeting host cannot be demoted or revoked.");
        }
        else if (accessLevel == MeetingAccessLevel.Host)
            throw new InvalidOperationException("Host transfer is not supported.");
        if (badgeDefinitionId.HasValue && !_badges.Any(x => x.Id == badgeDefinitionId && !x.IsDeleted))
            throw new InvalidOperationException("Meeting badge not found.");
        participant.Update(accessLevel, badgeDefinitionId, state);
        MarkAsUpdated();
    }

    public MeetingAccessLink AddAccessLink(string tokenHash, MeetingAccessLinkMode mode,
        string? lockedEmail, MeetingAccessLevel defaultAccessLevel, int? badgeDefinitionId,
        DateTime expiresAtUtc, int? maximumUses)
    {
        if (defaultAccessLevel == MeetingAccessLevel.Host)
            throw new InvalidOperationException("Access links cannot grant host access.");
        if (mode == MeetingAccessLinkMode.PrivateInvitation && string.IsNullOrWhiteSpace(lockedEmail))
            throw new InvalidOperationException("Private invitations require a locked email.");
        if (badgeDefinitionId.HasValue && !_badges.Any(x => x.Id == badgeDefinitionId && !x.IsDeleted))
            throw new InvalidOperationException("Meeting badge not found.");
        var link = new MeetingAccessLink(tokenHash, mode, lockedEmail, defaultAccessLevel,
            badgeDefinitionId, expiresAtUtc, maximumUses);
        _accessLinks.Add(link);
        MarkAsUpdated();
        return link;
    }

    public void RevokeAccessLink(int linkId, DateTime utcNow)
    {
        var link = _accessLinks.SingleOrDefault(x => x.Id == linkId && !x.IsDeleted)
            ?? throw new InvalidOperationException("Meeting access link not found.");
        link.Revoke(utcNow);
        MarkAsUpdated();
    }

    private void Apply(string title, string? description, DateTime? startUtc, DateTime? endUtc,
        string timeZone, bool lobbyEnabled, bool guestsAllowed, bool participantsCanPublish,
        bool participantsCanShareScreen, bool participantsCanEditNote, bool viewersCanChat,
        int retentionDays)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (startUtc.HasValue != endUtc.HasValue) throw new ArgumentException("A schedule requires both start and end.");
        if (startUtc.HasValue && endUtc <= startUtc) throw new ArgumentException("Scheduled end must be after start.");
        if (string.IsNullOrWhiteSpace(timeZone)) throw new ArgumentException("Time zone is required.", nameof(timeZone));
        if (retentionDays is < 1 or > 3650) throw new ArgumentOutOfRangeException(nameof(retentionDays));
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ScheduledStartUtc = startUtc.HasValue ? AsUtc(startUtc.Value) : null;
        ScheduledEndUtc = endUtc.HasValue ? AsUtc(endUtc.Value) : null;
        TimeZone = timeZone.Trim();
        LobbyEnabled = lobbyEnabled;
        GuestsAllowed = guestsAllowed;
        ParticipantsCanPublish = participantsCanPublish;
        ParticipantsCanShareScreen = participantsCanShareScreen;
        ParticipantsCanEditNote = participantsCanEditNote;
        ViewersCanChat = viewersCanChat;
        RetentionDays = retentionDays;
    }

    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
