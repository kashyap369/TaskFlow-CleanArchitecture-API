using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Models.Requests;
using TaskFlow.Application.Features.Attendance;

namespace TaskFlow.Api.Controllers.Organization;

[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[Route("api/attendance")]
[ApiController]
public sealed class AttendanceController : ControllerBase
{
    private const string SelfieRequiredCode = "ATTENDANCE_SELFIE_REQUIRED";
    private const string SelfieRequiredMessage = "Take a selfie to punch in or out.";

    private readonly IMediator _mediator;
    public AttendanceController(IMediator mediator) => _mediator = mediator;

    [HttpPost("organization/{organizationId:int}/punch-in")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> PunchIn(int organizationId, [FromForm] AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Photo is null)
            return BadRequest(new { code = SelfieRequiredCode, message = SelfieRequiredMessage });
        await using var photo = request.Photo.OpenReadStream();
        return Ok(await _mediator.Send(new PunchInCommand(organizationId, request.Note, request.Photo.Length, photo,
            request.Latitude, request.Longitude, request.AccuracyMeters),
            cancellationToken));
    }

    [HttpPost("organization/{organizationId:int}/punch-out")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> PunchOut(int organizationId, [FromForm] AttendancePunchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Photo is null)
            return BadRequest(new { code = SelfieRequiredCode, message = SelfieRequiredMessage });
        await using var photo = request.Photo.OpenReadStream();
        await _mediator.Send(new PunchOutCommand(organizationId, request.Note, request.Photo.Length, photo,
            request.Latitude, request.Longitude, request.AccuracyMeters), cancellationToken);
        return NoContent();
    }

    [HttpGet("organization/{organizationId:int}/me/today")]
    public async Task<IActionResult> GetMyToday(int organizationId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMyAttendanceTodayQuery(organizationId), cancellationToken));

    [HttpGet("organization/{organizationId:int}/me")]
    public async Task<IActionResult> GetMine(int organizationId, [FromQuery] DateOnly from, [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMyAttendanceQuery(organizationId, from, to), cancellationToken));

    [HttpGet("organization/{organizationId:int}/member/{userId:int}")]
    public async Task<IActionResult> GetMember(int organizationId, int userId, [FromQuery] DateOnly from,
        [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMemberAttendanceQuery(organizationId, userId, from, to), cancellationToken));

    [HttpGet("organization/{organizationId:int}/roster")]
    public async Task<IActionResult> GetRoster(int organizationId, [FromQuery] DateOnly? date, [FromQuery] int? teamId,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetAttendanceRosterQuery(organizationId, date, teamId), cancellationToken));

    [HttpGet("organization/{organizationId:int}/settings")]
    public async Task<IActionResult> GetSettings(int organizationId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetAttendanceSettingsQuery(organizationId), cancellationToken));

    [HttpPut("organization/{organizationId:int}/settings")]
    public async Task<IActionResult> UpdateSettings(int organizationId, UpdateAttendanceSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateAttendanceSettingsCommand(organizationId, request.TimeZone,
            request.AutoCloseAfterHours, request.PhotoRetentionDays), cancellationToken);
        return NoContent();
    }

    [HttpPut("{sessionId:int}/adjust")]
    public async Task<IActionResult> Adjust(int sessionId, AdjustAttendanceSessionRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new AdjustAttendanceSessionCommand(sessionId, request.PunchInAtUtc, request.PunchOutAtUtc,
            request.Reason), cancellationToken);
        return NoContent();
    }

    [HttpGet("{sessionId:int}/photo/{kind}")]
    public async Task<IActionResult> GetPhoto(int sessionId, string kind, CancellationToken cancellationToken)
    {
        var photo = await _mediator.Send(new GetAttendancePhotoQuery(sessionId, kind), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(photo.Content, photo.ContentType, photo.FileName, enableRangeProcessing: false);
    }
}
