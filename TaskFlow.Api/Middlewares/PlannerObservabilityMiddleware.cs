using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TaskFlow.Api.Observability;
using TaskFlow.Api.Options;

namespace TaskFlow.Api.Middlewares;

public sealed class PlannerObservabilityMiddleware(
    RequestDelegate next,
    ILogger<PlannerObservabilityMiddleware> logger,
    IOptionsMonitor<PlannerOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/planner") &&
            !context.Request.Path.StartsWithSegments("/api/admin/planner"))
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;
        var mutation = method is not ("GET" or "HEAD" or "OPTIONS");
        using var activity = PlannerTelemetry.ActivitySource.StartActivity("planner.http");
        activity?.SetTag("http.request.method", method);
        activity?.SetTag("url.path", path);

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var tags = new TagList
            {
                { "http.request.method", method },
                { "http.response.status_code", statusCode }
            };
            PlannerTelemetry.Requests.Add(1, tags);
            PlannerTelemetry.Duration.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            if (mutation) PlannerTelemetry.Mutations.Add(1, tags);
            if (statusCode >= 400) PlannerTelemetry.Failures.Add(1, tags);
            if (statusCode == StatusCodes.Status409Conflict) PlannerTelemetry.Conflicts.Add(1, tags);

            activity?.SetTag("http.response.status_code", statusCode);
            activity?.SetTag("taskflow.planner.mutation", mutation);

            var actorId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          context.User.FindFirstValue("sub") ?? "anonymous";
            var slow = stopwatch.ElapsedMilliseconds >= options.CurrentValue.SlowRequestMilliseconds;
            if (slow || statusCode >= 500)
            {
                logger.LogWarning(
                    "Planner operation slow_or_failed Method={Method} Path={Path} StatusCode={StatusCode} " +
                    "DurationMs={DurationMs} ActorId={ActorId} TraceId={TraceId}",
                    method, path, statusCode, stopwatch.ElapsedMilliseconds, actorId, context.TraceIdentifier);
            }
            else if (mutation)
            {
                logger.LogInformation(
                    "Planner audit Method={Method} Path={Path} StatusCode={StatusCode} DurationMs={DurationMs} " +
                    "ActorId={ActorId} TraceId={TraceId}",
                    method, path, statusCode, stopwatch.ElapsedMilliseconds, actorId, context.TraceIdentifier);
            }
        }
    }
}
