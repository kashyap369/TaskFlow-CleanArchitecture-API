namespace TaskFlow.Application.Contracts.Security
{
    public interface ICurrentUserService
    {
        int UserId { get; }

        string Email { get; }

        /// <summary>
        /// The caller's IP address. Never throws — returns
        /// "unknown" when it cannot be resolved. Used for
        /// refresh token auditing (CreatedByIp / RevokedByIp).
        /// </summary>
        string IpAddress { get; }

        /// <summary>
        /// True when the caller holds the <c>Admin</c> system role
        /// (from the JWT role claim). Never throws — returns false
        /// when there is no authenticated user.
        ///
        /// Used by <see cref="IOrganizationAccessGuard.EnsureUserAsync"/>
        /// so a platform admin can open any user profile. Before this,
        /// the AdminOnly user <i>list</i> and the org-scoped user
        /// <i>detail</i> disagreed: the seeded admin belongs to no
        /// organization, so it could list every account but open none.
        ///
        /// This is the <b>platform</b> role, not an organization role —
        /// it deliberately does NOT bypass organization data access.
        /// </summary>
        bool IsAdmin { get; }
    }
}
