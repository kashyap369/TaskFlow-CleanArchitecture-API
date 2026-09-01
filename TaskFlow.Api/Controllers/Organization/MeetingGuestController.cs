using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Features.Meetings;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Controllers.Organization;

[AllowAnonymous]
[ApiController]
[Route("api/meeting/guest")]
[ServiceFilter(typeof(MeetingGuestFeatureFilter))]
[EnableRateLimiting("meeting-guest")]
public sealed class MeetingGuestController(IMediator mediator, IOptions<MeetingSettings> settings) : ControllerBase
{
    [HttpPost("access/inspect")]
    public async Task<IActionResult> Inspect(InspectMeetingGuestAccessCommand command, CancellationToken ct) => Ok(await mediator.Send(command, ct));

    [HttpPost("access/request-code")]
    public async Task<IActionResult> RequestCode(RequestMeetingGuestCodeCommand command, CancellationToken ct)
    { await mediator.Send(command, ct); return NoContent(); }

    [HttpPost("access/verify-code")]
    public async Task<IActionResult> VerifyCode(VerifyMeetingGuestCodeRequest request, CancellationToken ct)
    {
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var value) ? value : null;
        var command = new VerifyMeetingGuestCodeCommand(request.Token, request.Email, request.Code,
            request.DisplayName, request.BindRegisteredAccount, userId, User.FindFirstValue(ClaimTypes.Email),
            settings.Value.GuestSessionMinutes);
        return Ok(await mediator.Send(command, ct));
    }

    [HttpGet("session")]
    public async Task<IActionResult> Session(CancellationToken ct) => Ok(await mediator.Send(
        new GetMeetingGuestSessionQuery(GuestSessionToken()), ct));

    [HttpPut("session/display-name")]
    public async Task<IActionResult> DisplayName(ConfirmGuestDisplayNameRequest request, CancellationToken ct) => Ok(await mediator.Send(
        new ConfirmMeetingGuestDisplayNameCommand(GuestSessionToken(), request.DisplayName), ct));

    [HttpPost("join-token")]
    public async Task<IActionResult> JoinToken(CancellationToken ct) => Ok(await mediator.Send(
        new GetGuestMeetingJoinTokenCommand(GuestSessionToken()), ct));

    [HttpPost("room/participants/{participantId:int}/remove")]
    public async Task<IActionResult> RemoveFromRoom(int participantId, CancellationToken ct)
    { await mediator.Send(new RemoveGuestMeetingRoomParticipantCommand(GuestSessionToken(), participantId), ct); return NoContent(); }

    [HttpPost("room/participants/{participantId:int}/mute")]
    public async Task<IActionResult> MuteInRoom(int participantId,
        MuteGuestMeetingRoomParticipantRequest request, CancellationToken ct)
    { await mediator.Send(new MuteGuestMeetingRoomParticipantCommand(GuestSessionToken(), participantId,
        request.ParticipantIdentity, request.TrackSid, request.Muted), ct); return NoContent(); }

    [HttpGet("messages")]
    public async Task<IActionResult> Messages([FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default) =>
        Ok(await mediator.Send(new GetGuestMeetingMessagesQuery(GuestSessionToken(), skip, take), ct));

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage(SendMeetingMessageRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new SendGuestMeetingMessageCommand(GuestSessionToken(), request.ClientMessageId, request.Body, request.ReplyToMessageId), ct));

    [HttpGet("note")]
    public async Task<IActionResult> Note(CancellationToken ct) => Ok(await mediator.Send(new GetGuestMeetingNoteQuery(GuestSessionToken()), ct));

    [HttpPut("note")]
    public async Task<IActionResult> UpdateNote(UpdateMeetingNoteRequest request, CancellationToken ct) =>
        Ok(await mediator.Send(new UpdateGuestMeetingNoteCommand(GuestSessionToken(), request.Content, request.ExpectedVersion), ct));

    [HttpGet("assets")]
    public async Task<IActionResult> Assets(CancellationToken ct) => Ok(await mediator.Send(new GetGuestMeetingAssetsQuery(GuestSessionToken()), ct));

    [HttpPost("assets")]
    public async Task<IActionResult> UploadAsset(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await mediator.Send(new UploadGuestMeetingAssetCommand(GuestSessionToken(), file.FileName,
            file.ContentType, file.Length, stream, settings.Value.MaxFileBytes), ct));
    }

    [HttpGet("assets/{assetId:int}")]
    public async Task<IActionResult> DownloadAsset(int assetId, CancellationToken ct)
    {
        var asset = await mediator.Send(new DownloadGuestMeetingAssetQuery(GuestSessionToken(), assetId), ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(asset.Content, asset.ContentType, asset.FileName, enableRangeProcessing: false);
    }

    [HttpDelete("assets/{assetId:int}")]
    public async Task<IActionResult> DeleteAsset(int assetId, CancellationToken ct)
    { await mediator.Send(new DeleteGuestMeetingAssetCommand(GuestSessionToken(), assetId), ct); return NoContent(); }

    [HttpGet("archive")]
    public async Task<IActionResult> Archive(CancellationToken ct) => Ok(await mediator.Send(new GetGuestMeetingArchiveQuery(GuestSessionToken()), ct));

    [HttpGet("recordings")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> Recordings(CancellationToken ct) => Ok(await mediator.Send(new GetGuestMeetingRecordingsQuery(GuestSessionToken()), ct));

    [HttpPost("recordings/{recordingId:int}/consent")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> RecordingConsent(int recordingId, MeetingRecordingConsentRequest request, CancellationToken ct) => Ok(await mediator.Send(new ConsentGuestMeetingRecordingCommand(GuestSessionToken(), recordingId, request.Accepted), ct));

    [HttpGet("recordings/{recordingId:int}/content")]
    [ServiceFilter(typeof(MeetingRecordingFeatureFilter))]
    public async Task<IActionResult> RecordingContent(int recordingId, CancellationToken ct)
    { var recording = await mediator.Send(new DownloadGuestMeetingRecordingQuery(GuestSessionToken(), recordingId), ct); Response.Headers.XContentTypeOptions = "nosniff"; return File(recording.Content, recording.ContentType, recording.FileName, enableRangeProcessing: true); }

    private string GuestSessionToken() => Request.Headers["X-Meeting-Guest-Session"].FirstOrDefault() ?? string.Empty;
}

public sealed record VerifyMeetingGuestCodeRequest(string Token, string Email, string Code,
    string DisplayName, bool BindRegisteredAccount = false);
public sealed record ConfirmGuestDisplayNameRequest(string DisplayName);
public sealed record MuteGuestMeetingRoomParticipantRequest(string ParticipantIdentity, string TrackSid, bool Muted);
