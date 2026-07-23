using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface IOrganizationPermissionRepository
    {
        Task<OrganizationPermission?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrganizationPermission>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            OrganizationPermission permission,
            CancellationToken cancellationToken = default);
    }
}
