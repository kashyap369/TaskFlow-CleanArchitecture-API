namespace TaskFlow.Domain.Constants
{
    /// <summary>
    /// The catalog of organization-level permission names.
    /// These are granted to organization roles and checked in
    /// handlers before permission-gated actions (create project,
    /// assign task, invite member, ...). Distinct from the
    /// system roles in <see cref="SystemRoleNames"/>.
    /// </summary>
    public static class OrganizationPermissionNames
    {
        public const string CreateProject = "CreateProject";

        public const string ManageProjects = "ManageProjects";

        public const string AssignTask = "AssignTask";

        public const string ManageTasks = "ManageTasks";

        public const string InviteMember = "InviteMember";

        public const string ManageMembers = "ManageMembers";

        public const string ManageRoles = "ManageRoles";

        public const string ManageTeams = "ManageTeams";

        public const string ViewReports = "ViewReports";

        /// <summary>
        /// Every known permission — used by the seeder to keep
        /// the OrganizationPermissions catalog table in sync.
        /// </summary>
        public static readonly string[] All =
        {
            CreateProject,
            ManageProjects,
            AssignTask,
            ManageTasks,
            InviteMember,
            ManageMembers,
            ManageRoles,
            ManageTeams,
            ViewReports
        };
    }
}
