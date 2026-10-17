namespace TaskFlow.Domain.DomainEvents.Organizations;

public sealed class AttendancePunchedInEvent : DomainEvent
{
    public int OrganizationId { get; }
    public int UserId { get; }
    public DateTime PunchInAtUtc { get; }

    public AttendancePunchedInEvent(int organizationId, int userId, DateTime punchInAtUtc) =>
        (OrganizationId, UserId, PunchInAtUtc) = (organizationId, userId, punchInAtUtc);
}

public sealed class AttendancePunchedOutEvent : DomainEvent
{
    public int OrganizationId { get; }
    public int UserId { get; }
    public DateTime PunchOutAtUtc { get; }

    public AttendancePunchedOutEvent(int organizationId, int userId, DateTime punchOutAtUtc) =>
        (OrganizationId, UserId, PunchOutAtUtc) = (organizationId, userId, punchOutAtUtc);
}

public sealed class AttendanceAdjustedEvent : DomainEvent
{
    public int OrganizationId { get; }
    public int UserId { get; }
    public int AttendanceSessionId { get; }
    public int AdjustedByUserId { get; }

    public AttendanceAdjustedEvent(int organizationId, int userId, int attendanceSessionId, int adjustedByUserId) =>
        (OrganizationId, UserId, AttendanceSessionId, AdjustedByUserId) =
        (organizationId, userId, attendanceSessionId, adjustedByUserId);
}
