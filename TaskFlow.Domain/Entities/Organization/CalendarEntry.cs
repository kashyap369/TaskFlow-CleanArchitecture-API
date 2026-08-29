using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums.Organization;

namespace TaskFlow.Domain.Entities.Organization;

public sealed class CalendarEntry : AuditableEntity, IAggregateRoot
{
    public int OrganizationId { get; private set; }
    public CalendarEntryKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public bool IsAllDay { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public int? MemberUserId { get; private set; }
    public CalendarRecurrenceFrequency RecurrenceFrequency { get; private set; }
    public int RecurrenceInterval { get; private set; }
    public DateOnly? RecurrenceUntil { get; private set; }
    public int CreatedByUserId { get; private set; }

    private CalendarEntry() { }

    public CalendarEntry(int organizationId, CalendarEntryKind kind, string title, string? description,
        DateTime startsAtUtc, DateTime endsAtUtc, bool isAllDay, string timeZone, int? memberUserId,
        CalendarRecurrenceFrequency recurrenceFrequency, int recurrenceInterval,
        DateOnly? recurrenceUntil, int createdByUserId)
    {
        OrganizationId = organizationId;
        CreatedByUserId = createdByUserId;
        Apply(kind, title, description, startsAtUtc, endsAtUtc, isAllDay, timeZone, memberUserId,
            recurrenceFrequency, recurrenceInterval, recurrenceUntil);
    }

    public void Update(CalendarEntryKind kind, string title, string? description, DateTime startsAtUtc,
        DateTime endsAtUtc, bool isAllDay, string timeZone, int? memberUserId,
        CalendarRecurrenceFrequency recurrenceFrequency, int recurrenceInterval, DateOnly? recurrenceUntil)
    {
        Apply(kind, title, description, startsAtUtc, endsAtUtc, isAllDay, timeZone, memberUserId,
            recurrenceFrequency, recurrenceInterval, recurrenceUntil);
        MarkAsUpdated();
    }

    private void Apply(CalendarEntryKind kind, string title, string? description, DateTime startsAtUtc,
        DateTime endsAtUtc, bool isAllDay, string timeZone, int? memberUserId,
        CalendarRecurrenceFrequency recurrenceFrequency, int recurrenceInterval, DateOnly? recurrenceUntil)
    {
        if (OrganizationId <= 0) throw new ArgumentOutOfRangeException(nameof(OrganizationId));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        if (endsAtUtc <= startsAtUtc) throw new ArgumentException("End must be after start.", nameof(endsAtUtc));
        if (kind == CalendarEntryKind.MemberLeave && memberUserId is null)
            throw new ArgumentException("Member leave requires a member.", nameof(memberUserId));
        if (kind != CalendarEntryKind.MemberLeave && memberUserId is not null)
            throw new ArgumentException("Only member leave can target a member.", nameof(memberUserId));
        if (kind != CalendarEntryKind.OrganizationEvent && !isAllDay)
            throw new ArgumentException("Leave and holidays must be all-day.", nameof(isAllDay));
        if (isAllDay && (startsAtUtc.TimeOfDay != TimeSpan.Zero || endsAtUtc.TimeOfDay != TimeSpan.Zero))
            throw new ArgumentException("All-day boundaries must use UTC midnight.", nameof(isAllDay));
        if (recurrenceFrequency == CalendarRecurrenceFrequency.None && recurrenceUntil is not null)
            throw new ArgumentException("A non-recurring entry cannot have a recurrence end.", nameof(recurrenceUntil));

        Kind = kind;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);
        EndsAtUtc = DateTime.SpecifyKind(endsAtUtc, DateTimeKind.Utc);
        IsAllDay = isAllDay;
        TimeZone = string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();
        MemberUserId = memberUserId;
        RecurrenceFrequency = recurrenceFrequency;
        RecurrenceInterval = recurrenceFrequency == CalendarRecurrenceFrequency.None ? 1 : recurrenceInterval;
        RecurrenceUntil = recurrenceUntil;
    }
}
