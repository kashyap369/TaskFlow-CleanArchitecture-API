using System.Diagnostics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Filters;
using TaskFlow.Application.Common.Observability;
using TaskFlow.Application.Contracts.Meetings;
using TaskFlow.Application.Features.Meetings;

namespace TaskFlow.Api.Controllers.Organization;

[AllowAnonymous]
[ApiController]
[Route("api/meeting/webhooks/livekit")]
[ServiceFilter(typeof(MeetingFeatureFilter))]
[EnableRateLimiting("meeting-webhook")]
public sealed class MeetingWebhookController(
    IMediator mediator,
    IMeetingMediaProvider mediaProvider,
    ILogger<MeetingWebhookController> logger) : ControllerBase
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
        catch (Exception exception)
        {
            // Phase 7 / P7.4. A refused delivery is silent by design — LiveKit is told 401 and
            // nothing in TaskFlow changes — which is exactly why it needs a counter and a log line.
            // A rotated API secret or a clock skew shows up here first, and every later symptom
            // (attendance missing, recordings stuck "processing") is downstream of it. The body is
            // never logged: it names rooms and participant identities.
            Count(MeetingWebhookOutcomes.RejectedSignature);
            logger.LogWarning(
                "Meeting webhook rejected Reason=signature Exception={ExceptionType} TraceId={TraceId}",
                exception.GetType().Name, HttpContext.TraceIdentifier);
            return Unauthorized();
        }

        try
        {
            await mediator.Send(new ProcessMeetingProviderWebhookCommand(webhook), ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Count(MeetingWebhookOutcomes.RejectedProcessing);
            logger.LogError(exception,
                "Meeting webhook processing failed EventType={EventType} TraceId={TraceId}",
                webhook.EventType, HttpContext.TraceIdentifier);
            throw;
        }

        // The accepted / duplicate / ignored outcomes are counted by the handler, which is the only
        // place that knows which of the three happened. Counting "accepted" here as well would
        // double every delivery and make a stream of ignored events look like healthy traffic.
        return Ok();
    }

    private static void Count(string outcome) =>
        MeetingTelemetry.Webhooks.Add(1, new TagList { { MeetingTelemetry.Tags.Outcome, outcome } });
}
