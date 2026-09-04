using MediatR;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Application.Features.Meetings;

/// <summary>
/// Platform-administration view of the meeting media stack as the running
/// process sees it. Authorized as AdminOnly by the controller: it is a
/// deployment question, not organization-scoped data, so no access-scope
/// marker applies. It reads configuration rather than the database, which is
/// why it uses no Dapper connection.
/// </summary>
public sealed record GetMeetingReadinessQuery : IRequest<MeetingReadinessReport>;

public sealed class GetMeetingReadinessQueryHandler(IMeetingReadinessProbe probe)
    : IRequestHandler<GetMeetingReadinessQuery, MeetingReadinessReport>
{
    public Task<MeetingReadinessReport> Handle(GetMeetingReadinessQuery request, CancellationToken ct)
        => Task.FromResult(probe.Describe());
}
