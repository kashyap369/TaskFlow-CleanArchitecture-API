namespace TaskFlow.Application.Contracts.Meetings;

/// <summary>
/// Meeting policy the Application layer needs but which is owned by configuration in Infra.
/// </summary>
public interface IMeetingPolicy
{
    /// <summary>
    /// Minimum attendance, in seconds, before the provider's room-closed event may end a meeting.
    /// Guards against a failed call archiving a meeting nobody actually attended.
    /// </summary>
    int AutoEndMinimumSessionSeconds { get; }
}
