using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Infra.Meetings;

public sealed class MeetingPolicy(IOptions<MeetingSettings> options) : IMeetingPolicy
{
    public int AutoEndMinimumSessionSeconds => options.Value.AutoEndMinimumSessionSeconds;
}
