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
        private const string AdminEmail = "admin@taskflow.com";

        public static async Task SeedAsync(
            TaskFlowDbContext context,
            IPasswordHasher passwordHasher)
        {
            if (!await context.Users.AnyAsync())
            {
                var user = Domain.Entities.Identity.User.Register(
                    new FullName(
                        "Admin",
                        "User"),
                    new Domain.ValueObjects.Email(
                        AdminEmail),
                    new PhoneNumber(
                        "9999999999"),
                    passwordHasher.Hash("Admin@123"));

                user.VerifyEmail();

                context.Users.Add(user);

                await context.SaveChangesAsync();
            }

            await AssignAdminRoleAsync(context);
        }

        // Gives the seeded admin user the "Admin" system role.
        // Runs every startup, so it also fixes databases that
        // were created before roles existed.
        private static async Task AssignAdminRoleAsync(
            TaskFlowDbContext context)
        {
            var adminUser =
                await context.Users.FirstOrDefaultAsync(
                    x => x.Email.Value == AdminEmail);

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