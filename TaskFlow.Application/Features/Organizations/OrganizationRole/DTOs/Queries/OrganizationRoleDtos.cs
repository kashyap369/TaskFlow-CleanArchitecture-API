namespace TaskFlow.Application.Features.Organizations.OrganizationRole.DTOs.Queries
{
    public sealed class OrganizationRoleDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
    }

    public sealed class OrganizationRoleDetailDto
    {
        public int Id { get; init; }
        public int OrganizationId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public IReadOnlyList<string> Permissions { get; set; } =
            Array.Empty<string>();
    }
}
