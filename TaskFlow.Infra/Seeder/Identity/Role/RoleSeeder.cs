using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Identity.Role
{
    public static class RoleSeeder
    {
        public static async Task SeedAsync(
            TaskFlowDbContext context)
        {
            var roles = new[]
            {
                new
                {
                    Name = SystemRoleNames.Admin,
                    Description = "Full access to everything."
                },
                new
                {
                    Name = SystemRoleNames.Manager,
                    Description = "Can manage projects, tasks and members."
                },
                new
                {
                    Name = SystemRoleNames.User,
                    Description = "Default role for every registered user."
                }
            };

            foreach (var role in roles)
            {
                var exists =
                    await context.SystemRoles.AnyAsync(
                        x => x.Name == role.Name);

                if (!exists)
                {
                    context.SystemRoles.Add(
                        new SystemRole(
                            role.Name,
                            role.Description));
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
