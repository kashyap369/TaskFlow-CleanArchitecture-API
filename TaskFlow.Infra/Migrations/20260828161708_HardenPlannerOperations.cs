using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class HardenPlannerOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PlannerSceneRevisions_BoardId_CreatedAt",
                table: "PlannerSceneRevisions",
                columns: new[] { "BoardId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlannerSceneRevisions_BoardId_CreatedAt",
                table: "PlannerSceneRevisions");
        }
    }
}
