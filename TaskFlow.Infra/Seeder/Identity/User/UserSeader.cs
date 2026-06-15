using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Identity;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.ValueObjects;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Identity.User
{
    public static class UserSeeder
    {
        public static async Task SeedAsync(
            TaskFlowDbContext context)
        {
            if (await context.Users.AnyAsync())
                return;

            var user = TaskFlow.Domain.Entities.Identity.User.Register(
                new FullName(
                    "Admin",
                    "User"),
                new TaskFlow.Domain.ValueObjects.Email(
                    "admin@taskflow.com"),
                new PhoneNumber(
                    "9999999999"),
                "SeededPasswordHash",
                AccountType.Individual);

            user.VerifyEmail();

            context.Users.Add(user);

            await context.SaveChangesAsync();
        }
    }
}