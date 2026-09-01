using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Domain.Enums.Meetings;

namespace TaskFlow.Api.Controllers.Organization;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[ServiceFilter(typeof(MeetingFeatureFilter))]
[Route("api/meeting")]
[ApiController]
public sealed class MeetingController(IMediator mediator, Microsoft.Extensions.Options.IOptions<TaskFlow.Infra.Meetings.MeetingSettings> meetingSettings) : ControllerBase
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

    [HttpGet("{meetingId:int}/messages")]
    public async Task<IActionResult> Messages(int meetingId, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetMeetingMessagesQuery(meetingId, skip, take), ct));

    [HttpPost("{meetingId:int}/messages")]
    [EnableRateLimiting("meeting-collaboration-write")]
    public async Task<IActionResult> SendMessage(int meetingId, SendMeetingMessageRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SendMeetingMessageCommand(meetingId, request.ClientMessageId, request.Body, request.ReplyToMessageId), ct));

    [HttpGet("{meetingId:int}/note")]
    public async Task<IActionResult> Note(int meetingId, CancellationToken ct) => Ok(await mediator.Send(new GetMeetingNoteQuery(meetingId), ct));

    [HttpPut("{meetingId:int}/note")]
    [EnableRateLimiting("meeting-collaboration-write")]
    public async Task<IActionResult> UpdateNote(int meetingId, UpdateMeetingNoteRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateMeetingNoteCommand(meetingId, request.Content, request.ExpectedVersion), ct));

    [HttpGet("{meetingId:int}/assets")]
    public async Task<IActionResult> Assets(int meetingId, CancellationToken ct) => Ok(await mediator.Send(new GetMeetingAssetsQuery(meetingId), ct));

    [HttpPost("{meetingId:int}/assets")]
    [EnableRateLimiting("meeting-collaboration-upload")]
    public async Task<IActionResult> UploadAsset(int meetingId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await mediator.Send(new UploadMeetingAssetCommand(meetingId, file.FileName,
            file.ContentType, file.Length, stream, meetingSettings.Value.MaxFileBytes), ct));
    }

    [HttpGet("{meetingId:int}/assets/{assetId:int}")]
    public async Task<IActionResult> DownloadAsset(int meetingId, int assetId, CancellationToken ct)
    {
        var asset = await mediator.Send(new DownloadMeetingAssetQuery(meetingId, assetId), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(asset.Content, asset.ContentType, asset.FileName, enableRangeProcessing: false);
    }

    [HttpDelete("{meetingId:int}/assets/{assetId:int}")]
    public async Task<IActionResult> DeleteAsset(int meetingId, int assetId, CancellationToken ct)
    { await mediator.Send(new DeleteMeetingAssetCommand(meetingId, assetId), ct); return NoContent(); }

    [HttpGet("{meetingId:int}/archive")]
    public async Task<IActionResult> Archive(int meetingId, CancellationToken ct) => Ok(await mediator.Send(new GetMeetingArchiveQuery(meetingId), ct));

    [HttpGet("{meetingId:int}/recordings")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> Recordings(int meetingId, CancellationToken ct) => Ok(await mediator.Send(new GetMeetingRecordingsQuery(meetingId), ct));

    [HttpPost("{meetingId:int}/recordings")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> RequestRecording(int meetingId, CancellationToken ct) => Ok(await mediator.Send(new RequestMeetingRecordingCommand(meetingId, meetingSettings.Value.RecordingConsentTimeoutSeconds), ct));

    [HttpPost("{meetingId:int}/recordings/{recordingId:int}/consent")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> RecordingConsent(int meetingId, int recordingId, MeetingRecordingConsentRequest request, CancellationToken ct) => Ok(await mediator.Send(new ConsentMeetingRecordingCommand(meetingId, recordingId, request.Accepted), ct));

    [HttpPost("{meetingId:int}/recordings/{recordingId:int}/stop")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> StopRecording(int meetingId, int recordingId, CancellationToken ct) => Ok(await mediator.Send(new StopMeetingRecordingCommand(meetingId, recordingId), ct));

    [HttpGet("{meetingId:int}/recordings/{recordingId:int}/content")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> RecordingContent(int meetingId, int recordingId, CancellationToken ct)
    { var recording = await mediator.Send(new DownloadMeetingRecordingQuery(meetingId, recordingId), ct); Response.Headers.XContentTypeOptions = "nosniff"; return File(recording.Content, recording.ContentType, recording.FileName, enableRangeProcessing: true); }

    [HttpDelete("{meetingId:int}/recordings/{recordingId:int}")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> DeleteRecording(int meetingId, int recordingId, CancellationToken ct)
    { await mediator.Send(new DeleteMeetingRecordingCommand(meetingId, recordingId), ct); return NoContent(); }
}

public sealed record MuteMeetingRoomParticipantRequest(string ParticipantIdentity, string TrackSid, bool Muted);
public sealed record SendMeetingMessageRequest(Guid ClientMessageId, string Body, int? ReplyToMessageId = null);
public sealed record UpdateMeetingNoteRequest(string Content, int ExpectedVersion);
public sealed record MeetingRecordingConsentRequest(bool Accepted);
