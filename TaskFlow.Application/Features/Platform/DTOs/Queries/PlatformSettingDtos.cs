namespace TaskFlow.Application.Features.Platform.DTOs.Queries
{
    /// <summary>
    /// The platform-wide settings an admin can read and change.
    /// </summary>
    public sealed class PlatformSettingDto
    {
        public int Id { get; init; }
        public string ApplicationName { get; init; } = string.Empty;
        public string? SupportEmail { get; init; }
        public bool RegistrationOpen { get; init; }
        public bool MaintenanceMode { get; init; }
        public string? MaintenanceMessage { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
