using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface IOrganizationRoleRepository
    {
        Task<OrganizationRole?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the role including its granted permissions —
        /// use this before Grant/Revoke/HasPermission.
        /// </summary>
        Task<OrganizationRole?> GetByIdWithPermissionsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<OrganizationRole?> GetByNameAsync(
            int organizationId,
            string roleName,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            int organizationId,
            string roleName,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrganizationRole>> GetOrganizationRolesAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            OrganizationRole role,
            CancellationToken cancellationToken = default);

        void Update(
            OrganizationRole role);

        void Remove(
            OrganizationRole role);
    }
}