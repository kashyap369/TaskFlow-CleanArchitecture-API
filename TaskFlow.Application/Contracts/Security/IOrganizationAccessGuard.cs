namespace TaskFlow.Application.Contracts.Security
{
    /// <summary>
    /// Resource authorization. Ensures the current user is allowed
    /// to touch a resource — closing IDOR gaps where any
    /// authenticated user could reach another organization's data
    /// by guessing ids.
    ///
    /// Used on <b>both sides</b>: reads go through
    /// <c>AccessGuardBehavior</c> (a query implements a marker
    /// interface), while the task / subtask / work-log <b>command</b>
    /// handlers call <see cref="EnsureTaskAsync"/> directly. Before
    /// Phase 9 those commands enforced nothing at all, so any
    /// authenticated user could start, complete or delete any task
    /// by id — and personal tasks would have inherited that hole.
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

        /// <summary>
        /// Stricter than <see cref="EnsureOrganizationAsync"/>:
        /// the current user must be the organization <b>owner</b>,
        /// not merely an active member. Used by the organization
        /// update / delete commands, which have no business being
        /// reachable by every member — before Phase 10 they had
        /// no authorization at all, so any authenticated user could
        /// rename or delete any organization by id.
        /// </summary>
        Task EnsureOrganizationOwnerAsync(
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
