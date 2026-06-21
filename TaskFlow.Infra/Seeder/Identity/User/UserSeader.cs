using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts.Security;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Identity.User
{
    public static class UserSeeder
    {
        public static async Task SeedAsync(
            TaskFlowDbContext context,
            IPasswordHasher passwordHasher)
        {
            if (await context.Users.AnyAsync())
                return;

            var user =Domain.Entities.Identity.User.Register(
                new FullName(
                    "Admin",
                    "User"),
                new Domain.ValueObjects.Email(
                    "admin@taskflow.com"),
                new PhoneNumber(
                    "9999999999"),
                passwordHasher.Hash("Admin@123"));

            user.VerifyEmail();

            context.Users.Add(user);

            await context.SaveChangesAsync();
        }
    }
}