using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Application.Features.Meetings;

public sealed record MeetingBadgeInput(string Label, string Color, string? Icon);
public sealed record MeetingListItemDto(int Id, int OrganizationId, string Title, string? Description,
    MeetingStatus Status, DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc, string TimeZone,
    DateTime? ActualStartUtc, DateTime? ActualEndUtc, int CreatedByUserId, string CreatorName,
    int ParticipantCount);
public sealed record MeetingPageDto(IReadOnlyList<MeetingListItemDto> Items, int Total, int Skip, int Take);
public sealed record MeetingDetailDto(int Id, int OrganizationId, int CreatedByUserId, string CreatorName,
    string Title, string? Description, DateTime? ScheduledStartUtc, DateTime? ScheduledEndUtc,
    string TimeZone, MeetingStatus Status, DateTime? ActualStartUtc, DateTime? ActualEndUtc,
    bool LobbyEnabled, bool GuestsAllowed, bool ParticipantsCanPublish,
    bool ParticipantsCanShareScreen, bool ParticipantsCanEditNote, bool ViewersCanChat,
    int RetentionDays, bool CanManage, IReadOnlyList<MeetingBadgeDto> Badges,
    IReadOnlyList<MeetingParticipantDto> Participants);
public sealed record MeetingBadgeDto(int Id, string Label, string Color, string? Icon);
public sealed record MeetingParticipantDto(int Id, int? UserId, string? DisplayName, string? Email,
    MeetingAccessLevel AccessLevel, int? BadgeDefinitionId, MeetingParticipantState State);
public sealed record MeetingAccessLinkDto(int Id, MeetingAccessLinkMode Mode, string? LockedEmail,
    MeetingAccessLevel DefaultAccessLevel, int? BadgeDefinitionId, DateTime ExpiresAtUtc,
    int? MaximumUses, int UseCount, DateTime? RevokedAtUtc);
public sealed record CreatedMeetingAccessLinkDto(int Id, string Token, DateTime ExpiresAtUtc);
