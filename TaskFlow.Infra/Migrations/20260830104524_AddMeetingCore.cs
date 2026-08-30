using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ScheduledEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActualStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LobbyEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    GuestsAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    ParticipantsCanPublish = table.Column<bool>(type: "boolean", nullable: false),
                    ParticipantsCanShareScreen = table.Column<bool>(type: "boolean", nullable: false),
                    ParticipantsCanEditNote = table.Column<bool>(type: "boolean", nullable: false),
                    ViewersCanChat = table.Column<bool>(type: "boolean", nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Meetings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingBadgeDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Color = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Icon = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingBadgeDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingBadgeDefinitions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAccessLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    LockedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DefaultAccessLevel = table.Column<int>(type: "integer", nullable: false),
                    BadgeDefinitionId = table.Column<int>(type: "integer", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaximumUses = table.Column<int>(type: "integer", nullable: true),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAccessLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAccessLinks_MeetingBadgeDefinitions_BadgeDefinitionId",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "MeetingBadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeetingAccessLinks_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    BadgeDefinitionId = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_MeetingBadgeDefinitions_BadgeDefinition~",
                        column: x => x.BadgeDefinitionId,
                        principalTable: "MeetingBadgeDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingParticipants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MeetingAttendance",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    ProviderConnectionId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderParticipantSid = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAttendance_MeetingParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAttendance_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAccessLinks_BadgeDefinitionId",
                table: "MeetingAccessLinks",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAccessLinks_MeetingId_ExpiresAtUtc",
                table: "MeetingAccessLinks",
                columns: new[] { "MeetingId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAccessLinks_TokenHash",
                table: "MeetingAccessLinks",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendance_MeetingId_LeftAtUtc",
                table: "MeetingAttendance",
                columns: new[] { "MeetingId", "LeftAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendance_MeetingId_ProviderConnectionId",
                table: "MeetingAttendance",
                columns: new[] { "MeetingId", "ProviderConnectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendance_ParticipantId",
                table: "MeetingAttendance",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingBadgeDefinitions_MeetingId_Label",
                table: "MeetingBadgeDefinitions",
                columns: new[] { "MeetingId", "Label" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_BadgeDefinitionId",
                table: "MeetingParticipants",
                column: "BadgeDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId_NormalizedEmail",
                table: "MeetingParticipants",
                columns: new[] { "MeetingId", "NormalizedEmail" },
                filter: "\"NormalizedEmail\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_MeetingId_UserId",
                table: "MeetingParticipants",
                columns: new[] { "MeetingId", "UserId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingParticipants_UserId",
                table: "MeetingParticipants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CreatedByUserId",
                table: "Meetings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_OrganizationId_Status_ScheduledStartUtc",
                table: "Meetings",
                columns: new[] { "OrganizationId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_RoomName",
                table: "Meetings",
                column: "RoomName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingAccessLinks");

            migrationBuilder.DropTable(
                name: "MeetingAttendance");

            migrationBuilder.DropTable(
                name: "MeetingParticipants");

            migrationBuilder.DropTable(
                name: "MeetingBadgeDefinitions");

            migrationBuilder.DropTable(
                name: "Meetings");
        }
    }
}
