using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities.Platform;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Platform
{
    /// <summary>
    /// Creates the single platform-settings row on first startup and
    /// then leaves it alone — an admin's saved values must survive
    /// every restart, so this only ever inserts when the table is
    /// empty. Runs on every boot like the other seeders.
    /// </summary>
    public static class PlatformSettingSeeder
    {
        public static async Task SeedAsync(
            TaskFlowDbContext context)
        {
            var exists =
                await context.PlatformSettings.AnyAsync();

            if (exists)
                return;

            context.PlatformSettings.Add(
                new PlatformSetting(
                    applicationName: "TaskFlow",
                    supportEmail: null,
                    registrationOpen: true,
                    maintenanceMode: false,
                    maintenanceMessage: null));

            await context.SaveChangesAsync();
        }
    }
}
