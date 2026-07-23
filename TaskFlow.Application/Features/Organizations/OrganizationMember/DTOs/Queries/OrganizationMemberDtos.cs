namespace TaskFlow.Application.Features.Organizations.OrganizationMember.DTOs.Queries
{
    public sealed class OrganizationMemberDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public int UserId { get; init; }
        public string UserFullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public int OrganizationRoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime JoinedAt { get; init; }
    }
}
