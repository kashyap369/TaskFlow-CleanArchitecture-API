using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Domain.Interfaces.Identity.Users
{
    public interface ISystemRoleRepository
    {
        Task<SystemRole?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<SystemRole?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SystemRole>> GetAllAsync(
            CancellationToken cancellationToken = default);

        Task AddAsync(
            SystemRole systemRole,
            CancellationToken cancellationToken = default);
    }
}
