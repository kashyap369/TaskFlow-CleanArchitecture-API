using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Application.Features.Calendar;

public sealed record CalendarEntryDto(
    int Id,
    string OccurrenceId,
    int OrganizationId,
    CalendarEntryKind Kind,
    string Title,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    bool IsAllDay,
    string TimeZone,
    int? MemberUserId,
    string? MemberName,
    CalendarRecurrenceFrequency RecurrenceFrequency,
    int RecurrenceInterval,
    DateOnly? RecurrenceUntil);
