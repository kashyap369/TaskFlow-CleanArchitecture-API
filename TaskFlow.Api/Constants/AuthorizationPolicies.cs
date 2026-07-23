namespace TaskFlow.Api.Constants
{
    /// <summary>
    /// Policy names used with [Authorize(Policy = ...)].
    /// Which roles each policy allows is defined in
    /// Extensions/ServiceCollectionExtensions.cs.
    /// </summary>
    public static class AuthorizationPolicies
    {
        // Only Admin.
        public const string AdminOnly = "AdminOnly";

        // Admin or Manager.
        public const string ManagerAndAbove = "ManagerAndAbove";

        // Any logged-in user with at least one system role.
        public const string AllRoles = "AllRoles";
    }
}
