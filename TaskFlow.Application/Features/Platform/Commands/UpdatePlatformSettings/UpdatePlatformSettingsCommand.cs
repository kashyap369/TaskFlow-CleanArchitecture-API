using MediatR;

namespace TaskFlow.Application.Features.Platform.Commands.UpdatePlatformSettings
{
    /// <summary>
    /// Updates the platform settings singleton. Admin-only, enforced
    /// by the <c>AdminOnly</c> policy on the route — there is no
    /// organization to scope this to.
    /// </summary>
    public sealed record UpdatePlatformSettingsCommand(
        string ApplicationName,
        string? SupportEmail,
        bool RegistrationOpen,
        bool MaintenanceMode,
        string? MaintenanceMessage
    ) : IRequest;
}
