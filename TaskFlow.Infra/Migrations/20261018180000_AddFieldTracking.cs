using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive only: nine new columns (every one nullable or defaulted, so existing rows
            // stay valid and tracking starts switched off) and five new tables. Nothing existing
            // is altered or dropped. The API applies migrations at startup.
            migrationBuilder.AddColumn<bool>(
                name: "IsTracked",
                table: "AttendanceSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PunchInAccuracyMeters",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PunchInLatitude",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PunchInLongitude",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PunchOutAccuracyMeters",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PunchOutLatitude",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PunchOutLongitude",
                table: "AttendanceSessions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LocationRetentionDays",
                table: "AttendanceSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "TrackingEnabled",
                table: "AttendanceSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AttendanceTrackedRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationRoleId = table.Column<int>(type: "integer", nullable: false),
                    AddedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceTrackedRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldTrackingConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldTrackingConsents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocationPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "double precision", nullable: true),
                    SpeedMetersPerSecond = table.Column<double>(type: "double precision", nullable: true),
                    HeadingDegrees = table.Column<double>(type: "double precision", nullable: true),
                    IsMock = table.Column<bool>(type: "boolean", nullable: false),
                    BatteryPercent = table.Column<int>(type: "integer", nullable: true),
                    IsCharging = table.Column<bool>(type: "boolean", nullable: true),
                    IsBatterySaverOn = table.Column<bool>(type: "boolean", nullable: true),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackedRouteSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EncodedPolyline = table.Column<string>(type: "text", nullable: false),
                    DistanceMeters = table.Column<double>(type: "double precision", nullable: false),
                    PointCount = table.Column<int>(type: "integer", nullable: false),
                    IsSnapped = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedRouteSegments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrackStops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AttendanceSessionId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    ArrivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackStops", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceTrackedRoles_OrganizationId_OrganizationRoleId",
                table: "AttendanceTrackedRoles",
                columns: new[] { "OrganizationId", "OrganizationRoleId" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_FieldTrackingConsents_OrganizationId_UserId_PolicyVersion",
                table: "FieldTrackingConsents",
                columns: new[] { "OrganizationId", "UserId", "PolicyVersion" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPoints_AttendanceSessionId",
                table: "LocationPoints",
                column: "AttendanceSessionId",
                filter: "\"IsProcessed\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_LocationPoints_AttendanceSessionId_RecordedAtUtc",
                table: "LocationPoints",
                columns: new[] { "AttendanceSessionId", "RecordedAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocationPoints_OrganizationId_UserId_RecordedAtUtc",
                table: "LocationPoints",
                columns: new[] { "OrganizationId", "UserId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedRouteSegments_AttendanceSessionId_Sequence",
                table: "TrackedRouteSegments",
                columns: new[] { "AttendanceSessionId", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackedRouteSegments_OrganizationId_UserId_StartedAtUtc",
                table: "TrackedRouteSegments",
                columns: new[] { "OrganizationId", "UserId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackStops_AttendanceSessionId_ArrivedAtUtc",
                table: "TrackStops",
                columns: new[] { "AttendanceSessionId", "ArrivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceTrackedRoles");

            migrationBuilder.DropTable(
                name: "FieldTrackingConsents");

            migrationBuilder.DropTable(
                name: "LocationPoints");

            migrationBuilder.DropTable(
                name: "TrackedRouteSegments");

            migrationBuilder.DropTable(
                name: "TrackStops");

            migrationBuilder.DropColumn(
                name: "IsTracked",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchInAccuracyMeters",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchInLatitude",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchInLongitude",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchOutAccuracyMeters",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchOutLatitude",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "PunchOutLongitude",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LocationRetentionDays",
                table: "AttendanceSettings");

            migrationBuilder.DropColumn(
                name: "TrackingEnabled",
                table: "AttendanceSettings");
        }
    }
}
