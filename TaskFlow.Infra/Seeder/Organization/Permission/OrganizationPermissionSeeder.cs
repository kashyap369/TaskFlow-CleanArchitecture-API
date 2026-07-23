using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Constants;
using TaskFlow.Domain.Entities.Organization;
using TaskFlow.Infra.Persistence.Context;

namespace TaskFlow.Infra.Seeder.Organization.Permission
{
    /// <summary>
    /// Keeps the OrganizationPermissions catalog table in sync
    /// with the permission names defined in
    /// <see cref="OrganizationPermissionNames"/>. Runs on every
    /// startup and only inserts names that are missing, so
    /// adding a new permission constant is enough to have it
    /// appear in the catalog.
    /// </summary>
    public static class OrganizationPermissionSeeder
    {
        private static readonly IReadOnlyDictionary<string, string>
            Descriptions = new Dictionary<string, string>
            {
                [OrganizationPermissionNames.CreateProject] =
                    "Create new projects in the organization.",
                [OrganizationPermissionNames.ManageProjects] =
                    "Update, archive and delete projects.",
                [OrganizationPermissionNames.AssignTask] =
                    "Assign tasks to organization members.",
                [OrganizationPermissionNames.ManageTasks] =
                    "Create, update and delete tasks.",
                [OrganizationPermissionNames.InviteMember] =
                    "Invite new members by email.",
                [OrganizationPermissionNames.ManageMembers] =
                    "Activate, deactivate and remove members.",
                [OrganizationPermissionNames.ManageRoles] =
                    "Create roles and grant or revoke permissions.",
                [OrganizationPermissionNames.ManageTeams] =
                    "Create teams and manage their members.",
                [OrganizationPermissionNames.ViewReports] =
                    "View organization reports and dashboards."
            };

        public static async Task SeedAsync(
            TaskFlowDbContext context)
        {
            foreach (var name in OrganizationPermissionNames.All)
            {
                var exists =
                    await context.OrganizationPermissions.AnyAsync(
                        x => x.Name == name);

                if (exists)
                    continue;

                Descriptions.TryGetValue(
                    name,
                    out var description);

                context.OrganizationPermissions.Add(
                    new OrganizationPermission(
                        name,
                        description));
            }

            await context.SaveChangesAsync();
        }
    }
}
