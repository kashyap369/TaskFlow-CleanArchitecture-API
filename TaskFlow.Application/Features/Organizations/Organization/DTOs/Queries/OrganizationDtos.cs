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
}
