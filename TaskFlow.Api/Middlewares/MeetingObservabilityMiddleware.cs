using System.Diagnostics;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Observability;
using TaskFlow.Infra.Meetings;

namespace TaskFlow.Api.Middlewares;

/// <summary>
/// Phase 7 / P7.4. Edge instrumentation for every meeting route: one metric per request, one span,
/// and a log line for the requests worth reading.
///
/// The concrete path never appears in a metric tag or a log field. `/api/meeting/412/messages`
/// would give meeting 412 its own time series and put an organization's meeting id in a log
/// aggregator that many people can read; the route <i>template</i> answers every operational
/// question ("is chat slow", "are join tokens failing") without either cost. The meeting id is
/// attached to the span, where it is scoped to one trace an engineer is already looking at.
///
/// Guest routes carry their credential in the <c>X-Meeting-Guest-Session</c> header and their
/// access-link token in the body, so neither can leak through a path. Nothing here reads a header,
/// a body or a query string, and that is deliberate rather than incidental.
/// </summary>
public sealed class MeetingObservabilityMiddleware(
    RequestDelegate next,
    ILogger<MeetingObservabilityMiddleware> logger,
    IOptionsMonitor<MeetingSettings> settings)
{
    private const string MeetingRoot = "/api/meeting";
    private const string AdminMeetingRoot = "/api/admin/meetings";
    private const string GuestRoot = "/api/meeting/guest";
    private const string WebhookRoot = "/api/meeting/webhooks";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (!path.StartsWithSegments(MeetingRoot) && !path.StartsWithSegments(AdminMeetingRoot))
        {
            await next(context);
            return;
        }

        var method = context.Request.Method;
        var actor = ClassifyActor(context);
        var stopwatch = Stopwatch.StartNew();

        using var activity = MeetingTelemetry.ActivitySource.StartActivity("meeting.http");
        activity?.SetTag(MeetingTelemetry.Tags.Method, method);
        activity?.SetTag(MeetingTelemetry.Tags.Actor, actor);

        // The global exception handler sits *outside* this middleware, so a thrown refusal passes
        // through here with the response status still at its default 200. Counting that as a
        // success would report every refused meeting request as a healthy one — exactly backwards
        // for rules that alert on refusals. The status is therefore taken from the exception, using
        // the same mapping the handler will apply a moment later.
        Exception? failure = null;
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            stopwatch.Stop();
            var status = failure is null
                ? context.Response.StatusCode
                : (int)ExceptionHandlingMiddleware.StatusCodeFor(failure);
            var route = RouteTemplate(context);
            var statusClass = MeetingTelemetry.ClassifyStatus(status);

            var tags = new TagList
            {
                { MeetingTelemetry.Tags.Route, route },
                { MeetingTelemetry.Tags.Method, method },
                { MeetingTelemetry.Tags.StatusCode, status },
                { MeetingTelemetry.Tags.StatusClass, statusClass },
                { MeetingTelemetry.Tags.Actor, actor }
            };

            MeetingTelemetry.Requests.Add(1, tags);
            MeetingTelemetry.RequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

            activity?.SetTag(MeetingTelemetry.Tags.Route, route);
            activity?.SetTag(MeetingTelemetry.Tags.StatusCode, status);
            activity?.SetTag(MeetingTelemetry.Tags.StatusClass, statusClass);
            // The one place an identifier is allowed: a span belongs to a single trace an engineer
            // has already opened, not to a shared metric series or a broadcast log stream.
            if (context.Request.RouteValues.TryGetValue("meetingId", out var meetingId))
            {
                activity?.SetTag("taskflow.meeting.id", meetingId);
            }

            var slow = stopwatch.ElapsedMilliseconds >= settings.CurrentValue.SlowRequestMilliseconds;
            var mutation = method is not ("GET" or "HEAD" or "OPTIONS");

            if (status >= 500 || slow)
            {
                logger.LogWarning(
                    "Meeting request degraded Route={Route} Method={Method} StatusCode={StatusCode} " +
                    "DurationMs={DurationMs} Actor={Actor} TraceId={TraceId}",
                    route, method, status, stopwatch.ElapsedMilliseconds, actor, context.TraceIdentifier);
            }
            else if (statusClass is MeetingTelemetry.StatusClasses.Denied
                     or MeetingTelemetry.StatusClasses.Throttled)
            {
                // Refusals are the abuse trail. One is routine; the runbook reads the rate, so each
                // one is recorded at Information rather than being dropped as an expected 403.
                logger.LogInformation(
                    "Meeting request refused Route={Route} Method={Method} StatusCode={StatusCode} " +
                    "Actor={Actor} TraceId={TraceId}",
                    route, method, status, actor, context.TraceIdentifier);
            }
            else if (mutation)
            {
                logger.LogInformation(
                    "Meeting audit Route={Route} Method={Method} StatusCode={StatusCode} " +
                    "DurationMs={DurationMs} Actor={Actor} TraceId={TraceId}",
                    route, method, status, stopwatch.ElapsedMilliseconds, actor, context.TraceIdentifier);
            }
        }
    }

    /// <summary>
    /// The route template, or a coarse bucket when no endpoint matched. Returning the raw path for
    /// an unmatched route would let a caller mint unlimited metric series by inventing URLs.
    /// </summary>
    private static string RouteTemplate(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern is RoutePattern pattern &&
        !string.IsNullOrWhiteSpace(pattern.RawText)
            ? pattern.RawText
            : "unmatched";

    private static string ClassifyActor(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments(WebhookRoot)) return MeetingTelemetry.Actors.Webhook;
        if (path.StartsWithSegments(GuestRoot)) return MeetingTelemetry.Actors.Guest;
        return context.User.Identity?.IsAuthenticated == true
            ? MeetingTelemetry.Actors.Member
            : MeetingTelemetry.Actors.Anonymous;
    }
}
