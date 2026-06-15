using System.Diagnostics;

namespace TaskFlow.Api.Middlewares
{
    public sealed class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(
            HttpContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            var ipAddress =
                context.Connection.RemoteIpAddress?.ToString();

            var userAgent =
                context.Request.Headers.UserAgent.ToString();

            var traceId =
                context.TraceIdentifier;

            var method =
                context.Request.Method;

            var path =
                context.Request.Path;

            await _next(context);

            stopwatch.Stop();

            _logger.LogInformation(
                @"Request Information
                TraceId: {TraceId}
                Method: {Method}
                Path: {Path}
                StatusCode: {StatusCode}
                Duration: {Duration} ms
                IPAddress: {IPAddress}
                UserAgent: {UserAgent}",
                traceId,
                method,
                path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                ipAddress,
                userAgent);
        }
    }
}