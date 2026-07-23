using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class OrganizationPermissionRepository
        : IOrganizationPermissionRepository
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationPermissionRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizationPermission?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationPermissions
                .FirstOrDefaultAsync(
                    x => x.Name == name,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<OrganizationPermission>>
            GetAllAsync(
                CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationPermissions
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationPermissions
                .AsNoTracking()
                .AnyAsync(
                    x => x.Name == name,
                    cancellationToken);
        }

        public async Task AddAsync(
            OrganizationPermission permission,
            CancellationToken cancellationToken = default)
        {
            await _context.OrganizationPermissions
                .AddAsync(
                    permission,
                    cancellationToken);
        }
    }
}
