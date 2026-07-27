using System.Text.Json;
using TaskFlow.Api.Models.Enums;
using TaskFlow.Api.Models.Responses;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Interfaces.Platform;

namespace TaskFlow.Api.Middlewares
{
    /// <summary>
    /// Refuses non-admin traffic with <b>503</b> while the platform
    /// setting <c>MaintenanceMode</c> is on.
    ///
    /// <para><b>Two deliberate escape hatches, so enabling this can
    /// never lock an admin out of their own platform:</b></para>
    /// <list type="number">
    /// <item><c>/api/auth/*</c> is always allowed — an admin has to be
    /// able to sign in <i>during</i> maintenance to turn it off, and
    /// the role is only known after authentication.</item>
    /// <item>Authenticated admins pass through everything, so the
    /// admin portal (including <c>PUT /api/admin/settings</c>) keeps
    /// working while everyone else is held off.</item>
    /// </list>
    ///
    /// <para>Runs <b>after</b> UseAuthentication so
    /// <c>User.IsInRole</c> is populated, and after the exception
    /// middleware so its own failures are still formatted.</para>
    /// </summary>
    public sealed class MaintenanceModeMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceModeMiddleware(
            RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IPlatformSettingRepository platformSettingRepository)
        {
            if (await ShouldBlockAsync(context, platformSettingRepository))
            {
                var settings =
                    await platformSettingRepository.GetAsync(
                        context.RequestAborted);

                context.Response.StatusCode =
                    StatusCodes.Status503ServiceUnavailable;

                context.Response.ContentType = "application/json";

                var payload = new ApiErrorResponse
                {
                    Code = "MAINTENANCE_MODE",
                    Message =
                        string.IsNullOrWhiteSpace(settings?.MaintenanceMessage)
                            ? "The platform is temporarily unavailable for maintenance."
                            : settings!.MaintenanceMessage!,
                    FailureReason =
                        FailureReason.BusinessRuleViolation.ToString(),
                    TraceId = context.TraceIdentifier
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(
                        payload,
                        new JsonSerializerOptions
                        {
                            PropertyNamingPolicy =
                                JsonNamingPolicy.CamelCase
                        }),
                    context.RequestAborted);

                return;
            }

            await _next(context);
        }

        private static async Task<bool> ShouldBlockAsync(
            HttpContext context,
            IPlatformSettingRepository platformSettingRepository)
        {
            // Auth endpoints stay open, always — see the class remarks.
            if (context.Request.Path.StartsWithSegments(
                    "/api/auth",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Admins are never held off.
            if (context.User?.IsInRole(SystemRoleNames.Admin) == true)
                return false;

            var settings =
                await platformSettingRepository.GetAsync(
                    context.RequestAborted);

            // No settings row (seeder never ran) => not in maintenance.
            // Failing open matches the behaviour from before the
            // setting existed.
            return settings?.MaintenanceMode == true;
        }
    }
}
