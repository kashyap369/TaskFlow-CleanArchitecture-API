using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Infra.Meetings;

public sealed class MeetingPolicy(IOptions<MeetingSettings> options) : IMeetingPolicy
{
    public int AutoEndMinimumSessionSeconds => options.Value.AutoEndMinimumSessionSeconds;

    public MeetingCapacity Capacity => new(
        options.Value.MaxParticipantsPerMeeting,
        options.Value.MaxConcurrentLiveMeetingsPerOrganization,
        options.Value.MaxConcurrentRecordings,
        options.Value.MaxMessagesPerMeeting,
        options.Value.MaxAssetsPerMeeting,
        options.Value.MaxFileBytes,
        options.Value.MaxStorageBytesPerMeeting);
}
