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

    /// <summary>
    /// The capacity TaskFlow declares and enforces. Phase 7 states measurable limits rather than
    /// implying unlimited scale, so every ceiling here is refused server-side, not merely hinted at
    /// in the UI. The values are conservative defaults until the owner approves the numbers in
    /// MEETINGS.md §12; see docs/MEETINGS-CAPACITY.md for what each one costs when reached.
    /// </summary>
    MeetingCapacity Capacity { get; }
}

/// <param name="MaxParticipantsPerMeeting">Seats on one meeting's roster, host included.</param>
/// <param name="MaxConcurrentLiveMeetingsPerOrganization">Meetings one organization may hold Live at once.</param>
/// <param name="MaxConcurrentRecordings">Egress jobs the deployment may run at once, across all organizations.</param>
/// <param name="MaxMessagesPerMeeting">Chat messages retained for one meeting.</param>
/// <param name="MaxAssetsPerMeeting">Files shared in one meeting.</param>
/// <param name="MaxFileBytes">Largest single upload.</param>
/// <param name="MaxStorageBytesPerMeeting">Total shared-file bytes for one meeting.</param>
public sealed record MeetingCapacity(
    int MaxParticipantsPerMeeting,
    int MaxConcurrentLiveMeetingsPerOrganization,
    int MaxConcurrentRecordings,
    int MaxMessagesPerMeeting,
    int MaxAssetsPerMeeting,
    long MaxFileBytes,
    long MaxStorageBytesPerMeeting);
