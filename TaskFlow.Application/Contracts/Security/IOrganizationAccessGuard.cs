namespace TaskFlow.Application.Contracts.Security
{
    /// <summary>
    /// Read-side authorization. Ensures the current user is
    /// allowed to see a resource before a query returns it —
    /// closing IDOR gaps where any authenticated user could
    /// read another organization's data by guessing ids.
    ///
    /// Access rule for organization data: the current user must
    /// be the organization owner or an active member. Project /
    /// task / team / role ids are resolved to their organization
    /// first. Each method throws
    /// <see cref="Exceptions.ForbiddenException"/> when access is
    /// denied and <see cref="Exceptions.NotFoundException"/> when
    /// the resource does not exist.
    /// </summary>
    public interface IOrganizationAccessGuard
    {
        Task EnsureOrganizationAsync(
            int organizationId,
            CancellationToken cancellationToken = default);

        Task EnsureProjectAsync(
            int projectId,
            CancellationToken cancellationToken = default);

        Task EnsureTaskAsync(
            int taskId,
            CancellationToken cancellationToken = default);

        Task EnsureTeamAsync(
            int teamId,
            CancellationToken cancellationToken = default);

        Task EnsureRoleAsync(
            int organizationRoleId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// A user profile is visible to the user themselves and
        /// to anyone who shares an organization with them.
        /// </summary>
        Task EnsureUserAsync(
            int targetUserId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// A member's report is visible to the member themselves
        /// and to the owner of an organization the member belongs
        /// to.
        /// </summary>
        Task EnsureMemberReportAsync(
            int targetUserId,
            CancellationToken cancellationToken = default);
    }
}
