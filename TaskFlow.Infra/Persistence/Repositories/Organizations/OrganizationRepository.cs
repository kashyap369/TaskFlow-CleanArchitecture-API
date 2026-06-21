using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class OrganizationRepository
        : IOrganizationRepository
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<Organization?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .Include(x => x.Members)
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<Organization?> GetByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .Include(x => x.Members)
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(
                    x => x.Name == name,
                    cancellationToken);
        }

        public async Task<Organization?> GetByOwnerUserIdAsync(
            int ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .Include(x => x.Members)
                .Include(x => x.Roles)
                .FirstOrDefaultAsync(
                    x => x.OwnerUserId == ownerUserId,
                    cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .AnyAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .AnyAsync(
                    x => x.Name == name,
                    cancellationToken);
        }

        public async Task<bool> ExistsByOwnerUserIdAsync(
            int ownerUserId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .AnyAsync(
                    x => x.OwnerUserId == ownerUserId,
                    cancellationToken);
        }

        public async Task<bool> IsMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.UserId == userId
                      && x.IsActive,
                    cancellationToken);
        }

        public async Task<bool> IsOwnerAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Organizations
                .AnyAsync(
                    x => x.Id == organizationId
                      && x.OwnerUserId == userId,
                    cancellationToken);
        }

        public async Task AddAsync(
            Organization organization,
            CancellationToken cancellationToken = default)
        {
            await _context.Organizations
                .AddAsync(
                    organization,
                    cancellationToken);
        }

        public void Update(
            Organization organization)
        {
            _context.Organizations.Update(
                organization);
        }

        public void Remove(
            Organization organization)
        {
            organization.SoftDelete();

            _context.Organizations.Update(
                organization);
        }
    }
}