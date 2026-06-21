using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class OrganizationRoleRepository
        : IOrganizationRoleRepository
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationRoleRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizationRole?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationRoles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<OrganizationRole?> GetByNameAsync(
            int organizationId,
            string roleName,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationRoles
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId
                      && x.Name == roleName,
                    cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationRoles
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            int organizationId,
            string roleName,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationRoles
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.Name == roleName,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<OrganizationRole>>
            GetOrganizationRolesAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationRoles
                .Where(x => x.OrganizationId == organizationId)
                .OrderBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            OrganizationRole role,
            CancellationToken cancellationToken = default)
        {
            await _context.OrganizationRoles
                .AddAsync(
                    role,
                    cancellationToken);
        }

        public void Update(
            OrganizationRole role)
        {
            _context.OrganizationRoles
                .Update(role);
        }

        public void Remove(
            OrganizationRole role)
        {
            role.SoftDelete();

            _context.OrganizationRoles
                .Update(role);
        }
    }
}