namespace TaskFlow.Infra.Seeder.Identity.User
{
    /// <summary>
    /// The seeded administrator account, bound from the <c>Seed:Admin</c>
    /// configuration section.
    ///
    /// The defaults are the ones documented in CLAUDE.md and README, so a
    /// fresh database is usable with no configuration at all. Override
    /// <see cref="Email"/> and <see cref="Password"/> in any environment
    /// that is reachable from outside a developer machine.
    /// </summary>
    public sealed class AdminSeedOptions
    {
        public const string SectionName = "Seed:Admin";

        public string Email { get; set; } = "admin@taskflow.com";

        public string Password { get; set; } = "Admin@123";

        /// <summary>
        /// Re-hashes <see cref="Password"/> onto the existing admin account
        /// on every startup.
        ///
        /// Off by default, and it must stay that way in normal operation:
        /// the seeder otherwise only ever runs against an empty database,
        /// so an administrator who changed their own password would have it
        /// silently reverted on the next deploy. Turn it on for one boot to
        /// repair an account whose password is unknown, then turn it off.
        /// </summary>
        public bool ResetPasswordOnStartup { get; set; }
    }
}
