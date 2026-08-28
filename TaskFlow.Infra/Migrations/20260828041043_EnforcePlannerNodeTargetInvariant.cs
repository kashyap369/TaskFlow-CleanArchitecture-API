using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePlannerNodeTargetInvariant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes",
                sql: "(\"NodeType\" = 1 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL) OR (\"NodeType\" = 2 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL AND \"SubTaskId\" IS NULL) OR (\"NodeType\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NOT NULL) OR (\"NodeType\" IN (4, 5) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes");
        }
    }
}
