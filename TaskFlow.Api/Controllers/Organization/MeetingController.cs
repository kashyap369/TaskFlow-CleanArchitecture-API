using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Api.Controllers.Organization;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[ServiceFilter(typeof(MeetingFeatureFilter))]
[Route("api/meeting")]
[ApiController]
public sealed class MeetingController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateMeetingCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command, ct));

    [HttpGet("organization/{organizationId:int}")]
    public async Task<IActionResult> List(int organizationId, [FromQuery] DateTimeOffset fromUtc,
        [FromQuery] DateTimeOffset toUtc, [FromQuery] MeetingStatus? status, [FromQuery] string? search,
        [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetOrganizationMeetingsQuery(organizationId, fromUtc, toUtc,
            status, search, skip, take), ct));

    [HttpGet("{meetingId:int}")]
    public async Task<IActionResult> Detail(int meetingId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeetingDetailQuery(meetingId), ct));

    [HttpPut("{meetingId:int}")]
    public async Task<IActionResult> Update(int meetingId, UpdateMeetingCommand command, CancellationToken ct)
    { await mediator.Send(command with { Id = meetingId }, ct); return NoContent(); }

    [HttpPost("{meetingId:int}/start")]
    public async Task<IActionResult> Start(int meetingId, CancellationToken ct)
    { await mediator.Send(new StartMeetingCommand(meetingId), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/end")]
    public async Task<IActionResult> End(int meetingId, CancellationToken ct)
    { await mediator.Send(new EndMeetingCommand(meetingId), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/join-token")]
    public async Task<IActionResult> JoinToken(int meetingId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeetingJoinTokenCommand(meetingId), ct));

    [HttpPost("{meetingId:int}/room/participants/{participantId:int}/remove")]
    public async Task<IActionResult> RemoveFromRoom(int meetingId, int participantId, CancellationToken ct)
    { await mediator.Send(new RemoveMeetingRoomParticipantCommand(meetingId, participantId), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/room/participants/{participantId:int}/mute")]
    public async Task<IActionResult> MuteInRoom(int meetingId, int participantId,
        MuteMeetingRoomParticipantRequest request, CancellationToken ct)
    { await mediator.Send(new MuteMeetingRoomParticipantCommand(meetingId, participantId,
        request.ParticipantIdentity, request.TrackSid, request.Muted), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/cancel")]
    public async Task<IActionResult> Cancel(int meetingId, CancellationToken ct)
    { await mediator.Send(new CancelMeetingCommand(meetingId), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/badges")]
    public async Task<IActionResult> AddBadge(int meetingId, AddMeetingBadgeCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command with { MeetingId = meetingId }, ct));

    [HttpPost("{meetingId:int}/participants")]
    public async Task<IActionResult> AddParticipant(int meetingId, AddMeetingParticipantCommand command, CancellationToken ct) =>
        Ok(await mediator.Send(command with { MeetingId = meetingId }, ct));

    [HttpPut("{meetingId:int}/participants/{participantId:int}")]
    public async Task<IActionResult> UpdateParticipant(int meetingId, int participantId,
        UpdateMeetingParticipantCommand command, CancellationToken ct)
    { await mediator.Send(command with { MeetingId = meetingId, ParticipantId = participantId }, ct); return NoContent(); }

    [HttpPost("{meetingId:int}/access-links")]
    public async Task<IActionResult> CreateAccessLink(int meetingId, CreateMeetingAccessLinkCommand command,
        CancellationToken ct) => Ok(await mediator.Send(command with { MeetingId = meetingId }, ct));

    [HttpGet("{meetingId:int}/access-links")]
    public async Task<IActionResult> GetAccessLinks(int meetingId, CancellationToken ct) =>
        Ok(await mediator.Send(new GetMeetingAccessLinksQuery(meetingId), ct));

    [HttpDelete("{meetingId:int}/access-links/{linkId:int}")]
    public async Task<IActionResult> RevokeAccessLink(int meetingId, int linkId, CancellationToken ct)
    { await mediator.Send(new RevokeMeetingAccessLinkCommand(meetingId, linkId), ct); return NoContent(); }

    [HttpPost("{meetingId:int}/access-links/{linkId:int}/rotate")]
    public async Task<IActionResult> RotateAccessLink(int meetingId, int linkId, CancellationToken ct) =>
        Ok(await mediator.Send(new RotateMeetingAccessLinkCommand(meetingId, linkId), ct));
}

public sealed record MuteMeetingRoomParticipantRequest(string ParticipantIdentity, string TrackSid, bool Muted);
