using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Identity.Users
{
    public sealed class OneTimeCodeRepository : IOneTimeCodeRepository
    {
        private readonly TaskFlowDbContext _context;

        public OneTimeCodeRepository(TaskFlowDbContext context)
        {
            _context = context;
        }

        public Task<OneTimeCode?> GetLatestAsync(
            int userId,
            OneTimeCodePurpose purpose,
            CancellationToken cancellationToken = default)
        {
            return _context.OneTimeCodes
                .Where(x => x.UserId == userId && x.Purpose == purpose)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<OneTimeCode>> GetUnconsumedAsync(
            int userId,
            OneTimeCodePurpose purpose,
            CancellationToken cancellationToken = default)
        {
            return await _context.OneTimeCodes
                .Where(x =>
                    x.UserId == userId &&
                    x.Purpose == purpose &&
                    x.ConsumedAt == null)
                .ToListAsync(cancellationToken);
        }

        public Task AddAsync(
            OneTimeCode oneTimeCode,
            CancellationToken cancellationToken = default)
        {
            return _context.OneTimeCodes
                .AddAsync(oneTimeCode, cancellationToken)
                .AsTask();
        }

        public void Update(OneTimeCode oneTimeCode)
        {
            _context.OneTimeCodes.Update(oneTimeCode);
        }
    }
}
