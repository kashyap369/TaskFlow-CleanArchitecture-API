namespace TaskFlow.Domain.Enums.Organization;

public enum CalendarEntryKind
{
    OrganizationEvent = 1,
    MemberLeave = 2,
    Holiday = 3
}

public enum CalendarRecurrenceFrequency
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}
