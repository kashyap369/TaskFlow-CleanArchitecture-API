namespace TaskFlow.Application.Features.Organizations.OrganizationRole.DTOs.Queries
{
    public sealed class OrganizationPermissionDto
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
    }
}
