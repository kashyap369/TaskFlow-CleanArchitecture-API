using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Interfaces.Identity.Users;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Identity.Users
{
    public sealed class UserRoleRepository
        : IUserRoleRepository
    {
        private readonly TaskFlowDbContext _context;

        public UserRoleRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<string>>
            GetRoleNamesByUserIdAsync(
                int userId,
                CancellationToken cancellationToken = default)
        {
            return await _context.UserRoles
                .Where(x => x.UserId == userId)
                .Join(
                    _context.SystemRoles,
                    userRole => userRole.SystemRoleId,
                    systemRole => systemRole.Id,
                    (userRole, systemRole) => systemRole.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int userId,
            int systemRoleId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserRoles
                .AsNoTracking()
                .AnyAsync(
                    x => x.UserId == userId
                      && x.SystemRoleId == systemRoleId,
                    cancellationToken);
        }

        public async Task AddAsync(
            UserRole userRole,
            CancellationToken cancellationToken = default)
        {
            await _context.UserRoles
                .AddAsync(
                    userRole,
                    cancellationToken);
        }

        public void Remove(
            UserRole userRole)
        {
            userRole.SoftDelete();

            _context.UserRoles.Update(userRole);
        }
    }
}
