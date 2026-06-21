using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface IOrganizationRepository
    {
        Task<Organization?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<Organization?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default);

        Task<Organization?> GetByOwnerUserIdAsync(
            int ownerUserId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            string name,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByOwnerUserIdAsync(
            int ownerUserId,
            CancellationToken cancellationToken = default);

        Task<bool> IsMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> IsOwnerAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Organization organization,
            CancellationToken cancellationToken = default);

        void Update(
            Organization organization);

        void Remove(
            Organization organization);
    }
}