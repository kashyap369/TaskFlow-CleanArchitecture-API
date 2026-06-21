using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.ValueObjects;

namespace TaskFlow.Domain.Interfaces.Identity.Users
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<User?> GetByEmailAsync(
            Email email,
            CancellationToken cancellationToken = default);

        Task<User?> GetByPhoneNumberAsync(
            PhoneNumber phoneNumber,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByEmailAsync(
            Email email,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsByPhoneNumberAsync(
            PhoneNumber phoneNumber,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<User>> GetByStatusAsync(
            UserStatus status,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<User>> GetUnverifiedUsersAsync(
            CancellationToken cancellationToken = default);

        Task AddAsync(
            User user,
            CancellationToken cancellationToken = default);

        void Update(
            User user);

        void Remove(
            User user);
    }
}