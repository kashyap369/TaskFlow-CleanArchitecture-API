using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingCollaborationArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    UploaderParticipantId = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScanStatus = table.Column<int>(type: "integer", nullable: false),
                    RetainUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAssets_MeetingParticipants_UploaderParticipantId",
                        column: x => x.UploaderParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingAssets_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    AuthorParticipantId = table.Column<int>(type: "integer", nullable: false),
                    ClientMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReplyToMessageId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingMessages_MeetingMessages_ReplyToMessageId",
                        column: x => x.ReplyToMessageId,
                        principalTable: "MeetingMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeetingMessages_MeetingParticipants_AuthorParticipantId",
                        column: x => x.AuthorParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingMessages_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    LastEditedByParticipantId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingNotes_MeetingParticipants_LastEditedByParticipantId",
                        column: x => x.LastEditedByParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MeetingNotes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MeetingNoteRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false),
                    NoteId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    EditorParticipantId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingNoteRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingNoteRevisions_MeetingNotes_NoteId",
                        column: x => x.NoteId,
                        principalTable: "MeetingNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MeetingNoteRevisions_MeetingParticipants_EditorParticipantId",
                        column: x => x.EditorParticipantId,
                        principalTable: "MeetingParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MeetingNoteRevisions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAssets_MeetingId_CreatedAt",
                table: "MeetingAssets",
                columns: new[] { "MeetingId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAssets_RetainUntilUtc_IsDeleted",
                table: "MeetingAssets",
                columns: new[] { "RetainUntilUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAssets_StorageKey",
                table: "MeetingAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAssets_UploaderParticipantId",
                table: "MeetingAssets",
                column: "UploaderParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMessages_AuthorParticipantId",
                table: "MeetingMessages",
                column: "AuthorParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMessages_MeetingId_AuthorParticipantId_ClientMessage~",
                table: "MeetingMessages",
                columns: new[] { "MeetingId", "AuthorParticipantId", "ClientMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMessages_MeetingId_CreatedAt_Id",
                table: "MeetingMessages",
                columns: new[] { "MeetingId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MeetingMessages_ReplyToMessageId",
                table: "MeetingMessages",
                column: "ReplyToMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNoteRevisions_EditorParticipantId",
                table: "MeetingNoteRevisions",
                column: "EditorParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNoteRevisions_MeetingId",
                table: "MeetingNoteRevisions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNoteRevisions_NoteId_Version",
                table: "MeetingNoteRevisions",
                columns: new[] { "NoteId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNotes_LastEditedByParticipantId",
                table: "MeetingNotes",
                column: "LastEditedByParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingNotes_MeetingId",
                table: "MeetingNotes",
                column: "MeetingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MeetingAssets");

            migrationBuilder.DropTable(
                name: "MeetingMessages");

            migrationBuilder.DropTable(
                name: "MeetingNoteRevisions");

            migrationBuilder.DropTable(
                name: "MeetingNotes");
        }
    }
}
