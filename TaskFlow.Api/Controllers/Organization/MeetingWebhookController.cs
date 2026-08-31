using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Features.Meetings;

namespace TaskFlow.Api.Controllers.Organization;

[AllowAnonymous]
[ApiController]
[Route("api/meeting/webhooks/livekit")]
[ServiceFilter(typeof(MeetingFeatureFilter))]
public sealed class MeetingWebhookController(IMediator mediator, IMeetingMediaProvider mediaProvider) : ControllerBase
{
    [HttpPost]
    [Consumes("application/webhook+json", "application/json")]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        MeetingProviderWebhook webhook;
        try
        {
            webhook = mediaProvider.VerifyWebhook(rawBody, Request.Headers.Authorization.ToString());
        }
        catch
        {
            return Unauthorized();
        }
        await mediator.Send(new ProcessMeetingProviderWebhookCommand(webhook), ct);
        return Ok();
    }
}
