namespace TaskFlow.Domain.Constants
{
    /// <summary>
    /// The application-level (system) role names.
    /// Used by the seeder, the login flow (JWT role claims)
    /// and the authorization policies in the Api layer.
    /// </summary>
    public static class SystemRoleNames
    {
        public const string Admin = "Admin";

        public const string Manager = "Manager";

        public const string User = "User";
    }
}
