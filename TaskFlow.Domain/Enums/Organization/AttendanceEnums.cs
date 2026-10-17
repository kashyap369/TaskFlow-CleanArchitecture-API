namespace TaskFlow.Domain.Enums.Organization;

public enum AttendanceSessionStatus
{
    Open = 1,
    Closed = 2,
    // Closed by the system because the punch-out was forgotten. Not counted as time worked.
    AutoClosed = 3,
    // Times corrected by someone holding ManageAttendance. The originals are kept on the row.
    Adjusted = 4
}
