using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingRecordingEgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingRecordings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    RequestedByParticipantId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ProviderEgressId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ConsentExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StoppedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadyAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecordings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRecordings_MeetingParticipants_RequestedByParticipan~",
                        column: x => x.RequestedByParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingRecordings_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingRecordingConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingRecordingId = table.Column<int>(type: "integer", nullable: false),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRecordingConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingRecordingConsents_MeetingParticipants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingRecordingConsents_MeetingRecordings_MeetingRecording~",
                        column: x => x.MeetingRecordingId,
                        principalTable: "MeetingRecordings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingRecordingConsents_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordingConsents_MeetingId_ParticipantId_CreatedAt",
                table: "MeetingRecordingConsents",
                columns: new[] { "MeetingId", "ParticipantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordingConsents_MeetingRecordingId_ParticipantId",
                table: "MeetingRecordingConsents",
                columns: new[] { "MeetingRecordingId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordingConsents_ParticipantId",
                table: "MeetingRecordingConsents",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordings_MeetingId_Status",
                table: "MeetingRecordings",
                columns: new[] { "MeetingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordings_ProviderEgressId",
                table: "MeetingRecordings",
                column: "ProviderEgressId",
                unique: true,
                filter: "\"ProviderEgressId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordings_RequestedByParticipantId",
                table: "MeetingRecordings",
                column: "RequestedByParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRecordings_StorageKey",
                table: "MeetingRecordings",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingRecordingConsents");

            migrationBuilder.DropTable(
                name: "MeetingRecordings");
        }
    }
}
