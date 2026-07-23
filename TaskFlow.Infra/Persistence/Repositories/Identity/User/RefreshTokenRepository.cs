using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Identity.Users
{
    public sealed class RefreshTokenRepository
        : IRefreshTokenRepository
    {
        private readonly TaskFlowDbContext _context;

        public RefreshTokenRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByTokenAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .FirstOrDefaultAsync(
                    x => x.Token == token,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<RefreshToken>>
            GetActiveByUserIdAsync(
                int userId,
                CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .Where(x => x.UserId == userId
                    && x.RevokedAt == null
                    && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            RefreshToken refreshToken,
            CancellationToken cancellationToken = default)
        {
            await _context.RefreshTokens
                .AddAsync(
                    refreshToken,
                    cancellationToken);
        }

        public void Update(
            RefreshToken refreshToken)
        {
            _context.RefreshTokens.Update(refreshToken);
        }
    }
}
