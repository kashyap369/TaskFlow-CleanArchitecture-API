using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Identity.Users
{
    public sealed class SystemRoleRepository
        : ISystemRoleRepository
    {
        private readonly TaskFlowDbContext _context;

        public SystemRoleRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<SystemRole?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.SystemRoles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<SystemRole?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var normalizedName =
                name.Trim().ToLower();

            return await _context.SystemRoles
                .FirstOrDefaultAsync(
                    x => x.Name.ToLower() == normalizedName,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<SystemRole>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            return await _context.SystemRoles
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            SystemRole systemRole,
            CancellationToken cancellationToken = default)
        {
            await _context.SystemRoles
                .AddAsync(
                    systemRole,
                    cancellationToken);
        }
    }
}
