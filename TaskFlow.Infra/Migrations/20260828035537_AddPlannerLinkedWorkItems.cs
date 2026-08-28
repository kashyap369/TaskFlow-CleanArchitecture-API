using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannerLinkedWorkItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ApproximateDurationWeeks",
                table: "Projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetAmount",
                table: "Projects",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BudgetCurrency",
                table: "Projects",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProblemStatement",
                table: "Projects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjectId",
                table: "PlannerNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubTaskId",
                table: "PlannerNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "PlannerNodes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_BoardId_ProjectId",
                table: "PlannerNodes",
                columns: new[] { "BoardId", "ProjectId" },
                unique: true,
                filter: "\"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_BoardId_SubTaskId",
                table: "PlannerNodes",
                columns: new[] { "BoardId", "SubTaskId" },
                unique: true,
                filter: "\"SubTaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_BoardId_TaskId",
                table: "PlannerNodes",
                columns: new[] { "BoardId", "TaskId" },
                unique: true,
                filter: "\"TaskId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_ProjectId",
                table: "PlannerNodes",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_SubTaskId",
                table: "PlannerNodes",
                column: "SubTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_TaskId",
                table: "PlannerNodes",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlannerNodes_Projects_ProjectId",
                table: "PlannerNodes",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannerNodes_SubTasks_SubTaskId",
                table: "PlannerNodes",
                column: "SubTaskId",
                principalTable: "SubTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannerNodes_Tasks_TaskId",
                table: "PlannerNodes",
                column: "TaskId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannerNodes_Projects_ProjectId",
                table: "PlannerNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannerNodes_SubTasks_SubTaskId",
                table: "PlannerNodes");

            migrationBuilder.DropForeignKey(
                name: "FK_PlannerNodes_Tasks_TaskId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_BoardId_ProjectId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_BoardId_SubTaskId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_BoardId_TaskId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_ProjectId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_SubTaskId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_TaskId",
                table: "PlannerNodes");

            migrationBuilder.DropColumn(
                name: "ApproximateDurationWeeks",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BudgetAmount",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "BudgetCurrency",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProblemStatement",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "PlannerNodes");

            migrationBuilder.DropColumn(
                name: "SubTaskId",
                table: "PlannerNodes");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "PlannerNodes");
        }
    }
}
