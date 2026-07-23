namespace TaskFlow.Application.Contracts.Security
{
    /// <summary>
    /// Answers organization-level permission questions used by
    /// command handlers before permission-gated actions
    /// (create project, assign task, manage teams, ...).
    /// The organization owner always passes; every other user
    /// must belong to a role that has been granted the
    /// permission. Distinct from the system-role policies
    /// enforced by the Api authorization layer.
    /// </summary>
    public interface IOrganizationPermissionChecker
    {
        Task<bool> HasPermissionAsync(
            int organizationId,
            int userId,
            string permissionName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Throws <see cref="Exceptions.ForbiddenException"/>
        /// when the user lacks the permission.
        /// </summary>
        Task EnsurePermissionAsync(
            int organizationId,
            int userId,
            string permissionName,
            CancellationToken cancellationToken = default);
    }
}
