using TaskFlow.Domain.Enums.Organizations;

namespace TaskFlow.Application.Features.Organizations.OrganizationInvitation.DTOs.Queries
{
    public sealed class OrganizationInvitationDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string OrganizationName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public int OrganizationRoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public InvitationStatus Status { get; init; }
        public DateTime ExpiryDate { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
