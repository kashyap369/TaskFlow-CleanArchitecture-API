using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveMeetingRecording : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordings_MeetingId",
                table: "MeetingRecordings",
                column: "MeetingId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" IN (1, 2, 3, 4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeetingRecordings_MeetingId",
                table: "MeetingRecordings");
        }
    }
}
