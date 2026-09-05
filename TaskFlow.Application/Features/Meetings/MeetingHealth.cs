using MediatR;
using TaskFlow.Application.Contracts.Meetings;

namespace TaskFlow.Application.Features.Meetings;

/// <summary>
/// Phase 7 / P7.4. Platform-administration view of what the meeting stack has been doing recently
/// and which alert rules are firing. Authorized as AdminOnly by the controller: like readiness, it
/// is a deployment question rather than organization-scoped data, so no access-scope marker applies.
///
/// It reads the in-process metric window rather than the database, which is why it uses no Dapper
/// connection and why its answer covers only this instance — a scaled-out deployment must ask each
/// instance, or scrape the same instruments into a collector. docs/MEETINGS-OBSERVABILITY.md says
/// which of the two a given environment is doing.
/// </summary>
public sealed record GetMeetingHealthQuery : IRequest<MeetingHealthReport>;

public sealed class GetMeetingHealthQueryHandler(IMeetingHealthSnapshot snapshot)
    : IRequestHandler<GetMeetingHealthQuery, MeetingHealthReport>
{
    public Task<MeetingHealthReport> Handle(GetMeetingHealthQuery request, CancellationToken ct)
        => Task.FromResult(snapshot.Describe(DateTime.UtcNow));
}
