using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannerResourcesAndAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes");

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "PlannerNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlannerResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerResources", x => x.Id);
                    table.CheckConstraint("CK_PlannerResources_Content", "(\"Kind\" = 1 AND \"Content\" IS NOT NULL AND \"Url\" IS NULL) OR (\"Kind\" = 2 AND \"Content\" IS NULL AND \"Url\" IS NOT NULL) OR (\"Kind\" = 3 AND \"Content\" IS NULL AND \"Url\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PlannerResources_PlannerBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "PlannerBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlannerAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ScanStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerAssets_PlannerResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "PlannerResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_BoardId_ResourceId",
                table: "PlannerNodes",
                columns: new[] { "BoardId", "ResourceId" },
                unique: true,
                filter: "\"ResourceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_ResourceId",
                table: "PlannerNodes",
                column: "ResourceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes",
                sql: "(\"NodeType\" = 1 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NULL) OR (\"NodeType\" = 2 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NULL) OR (\"NodeType\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NOT NULL AND \"ResourceId\" IS NULL) OR (\"NodeType\" IN (4, 5) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL AND \"ResourceId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerAssets_ProjectId_BoardId",
                table: "PlannerAssets",
                columns: new[] { "ProjectId", "BoardId" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerAssets_ResourceId",
                table: "PlannerAssets",
                column: "ResourceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerAssets_StorageKey",
                table: "PlannerAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlannerResources_BoardId_CreatedAt",
                table: "PlannerResources",
                columns: new[] { "BoardId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerResources_ProjectId_OwnerUserId",
                table: "PlannerResources",
                columns: new[] { "ProjectId", "OwnerUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PlannerNodes_PlannerResources_ResourceId",
                table: "PlannerNodes",
                column: "ResourceId",
                principalTable: "PlannerResources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannerNodes_PlannerResources_ResourceId",
                table: "PlannerNodes");

            migrationBuilder.DropTable(
                name: "PlannerAssets");

            migrationBuilder.DropTable(
                name: "PlannerResources");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_BoardId_ResourceId",
                table: "PlannerNodes");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_ResourceId",
                table: "PlannerNodes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "PlannerNodes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PlannerNodes_ExactlyOneTarget",
                table: "PlannerNodes",
                sql: "(\"NodeType\" = 1 AND \"ProjectId\" IS NOT NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL) OR (\"NodeType\" = 2 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NOT NULL AND \"SubTaskId\" IS NULL) OR (\"NodeType\" = 3 AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NOT NULL) OR (\"NodeType\" IN (4, 5) AND \"ProjectId\" IS NULL AND \"TaskId\" IS NULL AND \"SubTaskId\" IS NULL)");
        }
    }
}
