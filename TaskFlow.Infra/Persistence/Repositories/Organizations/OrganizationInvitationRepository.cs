using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Domain.Enums.Organizations;
using TaskFlow.Domain.Interfaces.Organizations;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Persistence.Repositories.Organizations
{
    public sealed class OrganizationInvitationRepository
        : IOrganizationInvitationRepository
    {
        private readonly TaskFlowDbContext _context;

        public OrganizationInvitationRepository(
            TaskFlowDbContext context)
        {
            _context = context;
        }

        public async Task<OrganizationInvitation?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationInvitations
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    cancellationToken);
        }

        public async Task<OrganizationInvitation?> GetPendingInvitationAsync(
            int organizationId,
            string email,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationInvitations
                .FirstOrDefaultAsync(
                    x => x.OrganizationId == organizationId
                      && x.Email == email
                      && x.Status == InvitationStatus.Pending,
                    cancellationToken);
        }

        public async Task<bool> ExistsPendingInvitationAsync(
            int organizationId,
            string email,
            CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationInvitations
                .AsNoTracking()
                .AnyAsync(
                    x => x.OrganizationId == organizationId
                      && x.Email == email
                      && x.Status == InvitationStatus.Pending,
                    cancellationToken);
        }

        public async Task<IReadOnlyList<OrganizationInvitation>>
            GetPendingInvitationsAsync(
                int organizationId,
                CancellationToken cancellationToken = default)
        {
            return await _context.OrganizationInvitations
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.Status == InvitationStatus.Pending)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(
            OrganizationInvitation invitation,
            CancellationToken cancellationToken = default)
        {
            await _context.OrganizationInvitations
                .AddAsync(
                    invitation,
                    cancellationToken);
        }

        public void Update(
            OrganizationInvitation invitation)
        {
            _context.OrganizationInvitations
                .Update(invitation);
        }

        public void Remove(
            OrganizationInvitation invitation)
        {
            invitation.SoftDelete();

            _context.OrganizationInvitations
                .Update(invitation);
        }
    }
}