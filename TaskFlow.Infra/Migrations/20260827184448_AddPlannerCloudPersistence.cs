using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannerCloudPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannerBoards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    SceneJson = table.Column<string>(type: "jsonb", nullable: false),
                    CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastOpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerBoards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerBoards_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannerNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerNodes_PlannerBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "PlannerBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannerSceneRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    SceneJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerSceneRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerSceneRevisions_PlannerBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "PlannerBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerBoards_OwnerUserId",
                table: "PlannerBoards",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerBoards_ProjectId",
                table: "PlannerBoards",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_BoardId_ElementId",
                table: "PlannerNodes",
                columns: new[] { "BoardId", "ElementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerSceneRevisions_BoardId_RevisionNumber",
                table: "PlannerSceneRevisions",
                columns: new[] { "BoardId", "RevisionNumber" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "PlannerBoards"
                    ("Id", "ProjectId", "OwnerUserId", "SceneJson", "CurrentRevision",
                     "CreatedAt", "UpdatedAt", "LastOpenedAt")
                SELECT
                    md5('taskflow-planner-board:' || p."Id"::text)::uuid,
                    p."Id",
                    p."CreatedByUserId",
                    '{"type":"excalidraw","version":2,"source":"taskflow","elements":[],"appState":{},"files":{}}'::jsonb,
                    0,
                    NOW(),
                    NOW(),
                    NULL
                FROM "Projects" p
                WHERE p."OrganizationId" IS NULL
                  AND p."IsDeleted" = FALSE
                ON CONFLICT ("ProjectId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannerNodes");

            migrationBuilder.DropTable(
                name: "PlannerSceneRevisions");

            migrationBuilder.DropTable(
                name: "PlannerBoards");
        }
    }
}
