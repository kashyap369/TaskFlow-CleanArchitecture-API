using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Identity.User
{
    public static class UserSeeder
    {
        public static async Task SeedAsync(
            TaskFlowDbContext context,
            IPasswordHasher passwordHasher,
            AdminSeedOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!await context.Users.AnyAsync())
            {
                var user = Domain.Entities.Identity.User.Register(
                    new FullName(
                        "Admin",
                        "User"),
                    new Domain.ValueObjects.Email(
                        options.Email),
                    new PhoneNumber(
                        "9999999999"),
                    passwordHasher.Hash(options.Password));

                user.VerifyEmail();

                // The administrator is not a new registration that needs
                // the product explained to it, so the first-run welcome
                // is closed out here rather than played on first sign-in.
                user.CompleteOnboarding();

                context.Users.Add(user);

                await context.SaveChangesAsync();
            }

            await EnsureAdminPasswordAsync(
                context,
                passwordHasher,
                options);

            await AssignAdminRoleAsync(
                context,
                options.Email);
        }

        /// <summary>
        /// Repairs the admin password, but only when explicitly asked to.
        ///
        /// The seeder above inserts nothing once the Users table has rows,
        /// so a database created with a different seed password keeps it
        /// forever. `Seed:Admin:ResetPasswordOnStartup` is the escape
        /// hatch for exactly that case; it is off by default so a deploy
        /// never quietly overwrites a password an administrator chose.
        /// </summary>
        private static async Task EnsureAdminPasswordAsync(
            TaskFlowDbContext context,
            IPasswordHasher passwordHasher,
            AdminSeedOptions options)
        {
            if (!options.ResetPasswordOnStartup)
                return;

            var adminUser =
                await context.Users.FirstOrDefaultAsync(
                    x => x.Email.Value == options.Email);

            if (adminUser is null)
                return;

            if (passwordHasher.Verify(
                    options.Password,
                    adminUser.PasswordHash))
                return;

            adminUser.ChangePassword(
                passwordHasher.Hash(options.Password));

            // A reset is pointless if the account cannot sign in, and a
            // database restored from before email verification existed
            // can be in exactly that state.
            adminUser.VerifyEmail();

            await context.SaveChangesAsync();
        }

        // Gives the seeded admin user the "Admin" system role.
        // Runs every startup, so it also fixes databases that
        // were created before roles existed.
        private static async Task AssignAdminRoleAsync(
            TaskFlowDbContext context,
            string adminEmail)
        {
            var adminUser =
                await context.Users.FirstOrDefaultAsync(
                    x => x.Email.Value == adminEmail);

            if (adminUser is null)
                return;

            var adminRole =
                await context.SystemRoles.FirstOrDefaultAsync(
                    x => x.Name == SystemRoleNames.Admin);

            if (adminRole is null)
                return;

            var alreadyAssigned =
                await context.UserRoles.AnyAsync(
                    x => x.UserId == adminUser.Id
                      && x.SystemRoleId == adminRole.Id);

            if (alreadyAssigned)
                return;

            context.UserRoles.Add(
                new UserRole(
                    adminUser.Id,
                    adminRole.Id));

            await context.SaveChangesAsync();
        }
    }
}
