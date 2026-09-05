using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Tests.Application;

/// <summary>
/// The declared meeting capacity, for tests that are not about capacity. Defaults match the
/// shipped configuration so a test never passes because a ceiling was accidentally infinite;
/// each value can be lowered to put one limit within reach of a small arrangement.
/// </summary>
internal sealed class MeetingTestPolicy : IMeetingPolicy
{
    public int AutoEndMinimumSessionSeconds { get; init; } = 30;
    public int MaxParticipantsPerMeeting { get; init; } = 50;
    public int MaxConcurrentLiveMeetingsPerOrganization { get; init; } = 10;
    public int MaxConcurrentRecordings { get; init; } = 1;
    public int MaxMessagesPerMeeting { get; init; } = 5000;
    public int MaxAssetsPerMeeting { get; init; } = 100;
    public long MaxFileBytes { get; init; } = 25 * 1024 * 1024;
    public long MaxStorageBytesPerMeeting { get; init; } = 250 * 1024 * 1024;

    public MeetingCapacity Capacity => new(MaxParticipantsPerMeeting,
        MaxConcurrentLiveMeetingsPerOrganization, MaxConcurrentRecordings, MaxMessagesPerMeeting,
        MaxAssetsPerMeeting, MaxFileBytes, MaxStorageBytesPerMeeting);
}
