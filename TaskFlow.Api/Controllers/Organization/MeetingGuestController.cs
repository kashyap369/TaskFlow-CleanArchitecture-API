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

    private string GuestSessionToken() => Request.Headers["X-Meeting-Guest-Session"].FirstOrDefault() ?? string.Empty;
}

public sealed record VerifyMeetingGuestCodeRequest(string Token, string Email, string Code,
    string DisplayName, bool BindRegisteredAccount = false);
public sealed record ConfirmGuestDisplayNameRequest(string DisplayName);
