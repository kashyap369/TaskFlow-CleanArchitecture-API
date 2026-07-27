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