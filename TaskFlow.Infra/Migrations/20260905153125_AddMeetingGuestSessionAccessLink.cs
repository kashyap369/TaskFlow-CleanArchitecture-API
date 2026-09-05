using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingGuestSessionAccessLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessLinkId",
                table: "MeetingGuestSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingGuestSessions_AccessLinkId_ExpiresAtUtc",
                table: "MeetingGuestSessions",
                columns: new[] { "AccessLinkId", "ExpiresAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_MeetingGuestSessions_MeetingAccessLinks_AccessLinkId",
                table: "MeetingGuestSessions",
                column: "AccessLinkId",
                principalTable: "MeetingAccessLinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MeetingGuestSessions_MeetingAccessLinks_AccessLinkId",
                table: "MeetingGuestSessions");

            migrationBuilder.DropIndex(
                name: "IX_MeetingGuestSessions_AccessLinkId_ExpiresAtUtc",
                table: "MeetingGuestSessions");

            migrationBuilder.DropColumn(
                name: "AccessLinkId",
                table: "MeetingGuestSessions");
        }
    }
}
