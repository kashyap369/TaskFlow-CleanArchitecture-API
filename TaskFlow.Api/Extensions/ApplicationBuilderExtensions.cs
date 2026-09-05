using TaskFlow.Api.Middlewares;

namespace TaskFlow.Api.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(
            this IApplicationBuilder app)
        {
            return app.UseMiddleware<ExceptionHandlingMiddleware>();
        }

        public static IApplicationBuilder UseRequestLogging(
            this IApplicationBuilder app)
        {
            return app.UseMiddleware<RequestLoggingMiddleware>();
        }

        public static IApplicationBuilder UsePlannerObservability(this IApplicationBuilder app) =>
            app.UseMiddleware<PlannerObservabilityMiddleware>();

        /// <summary>
        /// Must be registered <b>after</b> <c>UseAuthorization</c> so the matched endpoint is
        /// available: the middleware tags metrics with the route template rather than the concrete
        /// path, and without an endpoint every meeting request would be counted as "unmatched".
        /// </summary>
        public static IApplicationBuilder UseMeetingObservability(this IApplicationBuilder app) =>
            app.UseMiddleware<MeetingObservabilityMiddleware>();

        /// <summary>
        /// Must be registered <b>after</b> <c>UseAuthentication</c> —
        /// the middleware exempts admins, and the role claim is only
        /// populated once authentication has run.
        /// </summary>
        public static IApplicationBuilder UseMaintenanceMode(
            this IApplicationBuilder app)
        {
            return app.UseMiddleware<MaintenanceModeMiddleware>();
        }
    }
}
