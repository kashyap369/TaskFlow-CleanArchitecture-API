using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class OrganizationMemberRepository
        : IOrganizationMemberRepository
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationMemberRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizationMember?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<OrganizationMember?> GetMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId
                      && x.UserId == userId,
                    cancellationToken);
        }

        public async Task<bool> ExistsAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.UserId == userId,
                    cancellationToken);
        }

        public async Task<bool> IsActiveMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.UserId == userId
                      && x.IsActive,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<OrganizationMember>>
            GetOrganizationMembersAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .Where(x => x.OrganizationId == organizationId)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<OrganizationMember>>
            GetUserOrganizationsAsync(
                int userId,
                CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationMembers
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            OrganizationMember member,
            CancellationToken cancellationToken = default)
        {
            await _context.OrganizationMembers
                .AddAsync(
                    member,
                    cancellationToken);
        }

        public void Update(
            OrganizationMember member)
        {
            _context.OrganizationMembers
                .Update(member);
        }

        public void Remove(
            OrganizationMember member)
        {
            member.SoftDelete();

            _context.OrganizationMembers
                .Update(member);
        }
    }
}