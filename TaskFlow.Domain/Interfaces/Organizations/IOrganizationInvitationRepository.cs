using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface IOrganizationInvitationRepository
    {
        Task<OrganizationInvitation?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<OrganizationInvitation?> GetPendingInvitationAsync(
            int organizationId,
            string email,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsPendingInvitationAsync(
            int organizationId,
            string email,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrganizationInvitation>> GetPendingInvitationsAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            OrganizationInvitation invitation,
            CancellationToken cancellationToken = default);

        void Update(
            OrganizationInvitation invitation);

        void Remove(
            OrganizationInvitation invitation);
    }
}