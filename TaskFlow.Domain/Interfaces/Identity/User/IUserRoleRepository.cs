using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Domain.Interfaces.Identity.Users
{
    public interface IUserRoleRepository
    {
        Task<IReadOnlyList<string>> GetRoleNamesByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int userId,
            int systemRoleId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            UserRole userRole,
            CancellationToken cancellationToken = default);

        void Remove(
            UserRole userRole);
    }
}
