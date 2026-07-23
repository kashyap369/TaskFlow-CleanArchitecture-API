namespace TaskFlow.Application.Features.Organizations.Team.DTOs.Queries
{
    public sealed class TeamDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int MemberCount { get; init; }
    }

    public sealed class TeamMemberDto
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime JoinedAt { get; init; }
    }

    public sealed class TeamDetailDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public IReadOnlyList<TeamMemberDto> Members { get; set; } =
            Array.Empty<TeamMemberDto>();
    }
}
