using TaskFlow.Domain.Entities.Organization;

namespace TaskFlow.Domain.Interfaces.Organizations
{
    public interface IOrganizationMemberRepository
    {
        Task<OrganizationMember?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default);

        Task<OrganizationMember?> GetMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task<bool> IsActiveMemberAsync(
            int organizationId,
            int userId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrganizationMember>> GetOrganizationMembersAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<OrganizationMember>> GetUserOrganizationsAsync(
            int userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(
            OrganizationMember member,
            CancellationToken cancellationToken = default);

        void Update(
            OrganizationMember member);

        void Remove(
            OrganizationMember member);
    }
}