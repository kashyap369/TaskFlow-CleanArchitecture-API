using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Constants;
using TaskFlow.Api.Models.Requests;
using TaskFlow.Application.Features.Attendance;

namespace TaskFlow.Api.Controllers.Organization;

/// <summary>
/// Field (route) tracking of members whose role is tracked, only while punched in. Design:
/// Taskaro docs/FIELD-TRACKING.md; reference: docs/ATTENDANCE.md §8.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.AllRoles)]
[Route("api/attendance/organization/{organizationId:int}/tracking")]
[ApiController]
public sealed class FieldTrackingController : ControllerBase
{
    private readonly IMediator _mediator;
    public FieldTrackingController(IMediator mediator) => _mediator = mediator;

    [HttpGet("me/status")]
    public async Task<IActionResult> GetMyStatus(int organizationId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMyFieldTrackingStatusQuery(organizationId), cancellationToken));

    [HttpPost("consent")]
    public async Task<IActionResult> GiveConsent(int organizationId, GiveFieldTrackingConsentRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new GiveFieldTrackingConsentCommand(organizationId, request.PolicyVersion), cancellationToken);
        return NoContent();
    }

    [HttpDelete("consent")]
    public async Task<IActionResult> WithdrawConsent(int organizationId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new WithdrawFieldTrackingConsentCommand(organizationId), cancellationToken);
        return NoContent();
    }

    [HttpPost("points")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> RecordPoints(int organizationId, RecordLocationPointsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new RecordLocationPointsCommand(organizationId, request.AttendanceSessionId,
            (request.Points ?? []).Select(x => new LocationPointInput(x.RecordedAt, x.Latitude, x.Longitude,
                x.AccuracyMeters, x.SpeedMetersPerSecond, x.HeadingDegrees, x.IsMock, x.BatteryPercent, x.IsCharging,
                x.IsBatterySaverOn)).ToList()), cancellationToken));

    [HttpGet("me")]
    public async Task<IActionResult> GetMyDay(int organizationId, [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMyFieldTrackingDayQuery(organizationId, date), cancellationToken));

    [HttpGet("member/{userId:int}")]
    public async Task<IActionResult> GetMemberDay(int organizationId, int userId, [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetFieldTrackingDayQuery(organizationId, userId, date), cancellationToken));

    /// <summary>The stored fixes behind a day's route, sampled — the map's hover detail.</summary>
    [HttpGet("me/points")]
    public async Task<IActionResult> GetMyPoints(int organizationId, [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetMyFieldTrackingPointsQuery(organizationId, date), cancellationToken));

    [HttpGet("member/{userId:int}/points")]
    public async Task<IActionResult> GetMemberPoints(int organizationId, int userId, [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetFieldTrackingPointsQuery(organizationId, userId, date), cancellationToken));

    [HttpGet("live")]
    public async Task<IActionResult> GetLive(int organizationId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetLiveFieldTrackingQuery(organizationId), cancellationToken));

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(int organizationId, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetFieldTrackingSettingsQuery(organizationId), cancellationToken));

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(int organizationId, UpdateFieldTrackingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateFieldTrackingSettingsCommand(organizationId, request.TrackingEnabled,
            request.LocationRetentionDays, request.TrackedRoleIds ?? []), cancellationToken);
        return NoContent();
    }
}
