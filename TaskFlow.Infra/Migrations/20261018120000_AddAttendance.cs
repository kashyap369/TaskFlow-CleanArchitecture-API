using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only: two new tables, nothing existing is altered. The API applies
            // migrations at startup, so this runs against production on deploy.
            migrationBuilder.CreateTable(
                name: "AttendanceSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    WorkDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PunchInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PunchOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PunchInNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PunchOutNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PunchInPhotoKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PunchInPhotoContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PunchInPhotoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    PunchInPhotoSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PunchOutPhotoKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PunchOutPhotoContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PunchOutPhotoSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    PunchOutPhotoSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PhotosPurgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalPunchInAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginalPunchOutAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdjustedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AdjustmentReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdjustedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AutoCloseAfterHours = table.Column<int>(type: "integer", nullable: false),
                    PhotoRetentionDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_OrganizationId_UserId",
                table: "AttendanceSessions",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true,
                filter: "\"PunchOutAtUtc\" IS NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_OrganizationId_UserId_WorkDate",
                table: "AttendanceSessions",
                columns: new[] { "OrganizationId", "UserId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSessions_OrganizationId_WorkDate",
                table: "AttendanceSessions",
                columns: new[] { "OrganizationId", "WorkDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceSettings_OrganizationId",
                table: "AttendanceSettings",
                column: "OrganizationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceSessions");

            migrationBuilder.DropTable(
                name: "AttendanceSettings");
        }
    }
}
