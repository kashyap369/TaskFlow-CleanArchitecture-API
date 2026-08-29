using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EstimateMinutes",
                table: "Tasks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WeeklyCapacityMinutes",
                table: "OrganizationMembers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstimateMinutes",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "WeeklyCapacityMinutes",
                table: "OrganizationMembers");
        }
    }
}
