using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Platform;
using TaskFlow.Domain.Interfaces.Platform;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Platform
{
    public sealed class PlatformSettingRepository
        : IPlatformSettingRepository
    {
        private readonly TaskFlowDbContext _context;

        public PlatformSettingRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<PlatformSetting?> GetAsync(
            CancellationToken cancellationToken = default)
        {
            // Ordered by Id rather than filtered to SingletonId, so a
            // row that somehow exists under a different id is still
            // found instead of the API behaving as if unseeded.
            return await _context.PlatformSettings
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public void Update(
            PlatformSetting setting)
        {
            _context.PlatformSettings
                .Update(setting);
        }
    }
}
