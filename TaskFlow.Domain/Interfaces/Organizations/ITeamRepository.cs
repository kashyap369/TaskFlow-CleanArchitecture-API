using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface ITeamRepository
    {
        /// <summary>
        /// Loads the team including its members.
        /// </summary>
        Task<Team?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Team>> GetByOrganizationIdAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByNameAsync(
            int organizationId,
            string name,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            Team team,
            CancellationToken cancellationToken = default);

        void Update(
            Team team);

        void Remove(
            Team team);
    }
}
