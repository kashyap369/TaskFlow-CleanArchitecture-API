using TaskFlow.Domain.Entities.Identity;

namespace TaskFlow.Domain.Interfaces.Identity.Users
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByTokenAsync(
            string token,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<RefreshToken>> GetActiveByUserIdAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            RefreshToken refreshToken,
            CancellationToken cancellationToken = default);

        void Update(
            RefreshToken refreshToken);
    }
}
