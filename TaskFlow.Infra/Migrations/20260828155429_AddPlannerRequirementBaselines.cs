using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannerRequirementBaselines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequirementBaselines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    BaselineNumber = table.Column<int>(type: "integer", nullable: false),
                    FinalizedByUserId = table.Column<int>(type: "integer", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementBaselines_PlannerBoards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "PlannerBoards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    ParentEntityId = table.Column<int>(type: "integer", nullable: true),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OldValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    NewValuesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementChanges_RequirementBaselines_BaselineId",
                        column: x => x.BaselineId,
                        principalTable: "RequirementBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequirementSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BaselineId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<int>(type: "integer", nullable: false),
                    EntityId = table.Column<int>(type: "integer", nullable: false),
                    ParentEntityId = table.Column<int>(type: "integer", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequirementSnapshots_RequirementBaselines_BaselineId",
                        column: x => x.BaselineId,
                        principalTable: "RequirementBaselines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementBaselines_BoardId",
                table: "RequirementBaselines",
                column: "BoardId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementBaselines_ProjectId_BaselineNumber",
                table: "RequirementBaselines",
                columns: new[] { "ProjectId", "BaselineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RequirementChanges_BaselineId_ChangedAt",
                table: "RequirementChanges",
                columns: new[] { "BaselineId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementChanges_BaselineId_ChangeType",
                table: "RequirementChanges",
                columns: new[] { "BaselineId", "ChangeType" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementSnapshots_BaselineId_EntityType_EntityId",
                table: "RequirementSnapshots",
                columns: new[] { "BaselineId", "EntityType", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementChanges");

            migrationBuilder.DropTable(
                name: "RequirementSnapshots");

            migrationBuilder.DropTable(
                name: "RequirementBaselines");
        }
    }
}
