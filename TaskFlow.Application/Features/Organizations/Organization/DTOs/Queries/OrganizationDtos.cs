using TaskFlow.Domain.Enums.Organizations;

namespace TaskFlow.Application.Features.Organizations.Organization.DTOs.Queries
{
    public sealed class OrganizationDetailDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int OwnerUserId { get; init; }
        public OrganizationStatus Status { get; init; }
        public int MemberCount { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public sealed class OrganizationListItemDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int OwnerUserId { get; init; }
        public OrganizationStatus Status { get; init; }
    }

    /// <summary>
    /// A platform-wide organization row for the admin portal. Richer
    /// than <see cref="OrganizationListItemDto"/> — which feeds the org
    /// switcher and is deliberately thin — because an admin listing
    /// every workspace needs to tell them apart: who owns it, how big
    /// it is, when it was created.
    /// </summary>
    public sealed class AdminOrganizationListItemDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int OwnerUserId { get; init; }
        public string OwnerFullName { get; init; } = string.Empty;
        public string OwnerEmail { get; init; } = string.Empty;
        public OrganizationStatus Status { get; init; }
        public int MemberCount { get; init; }
        public int ProjectCount { get; init; }
        public int TaskCount { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
