using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannerTemplateLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateVersionId",
                table: "PlannerNodes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlannerTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ObjectType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Header = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    StrokeColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    DefaultWidth = table.Column<int>(type: "integer", nullable: false),
                    DefaultHeight = table.Column<int>(type: "integer", nullable: false),
                    VisibleFieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlannerTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ObjectType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Header = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BackgroundColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    StrokeColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    DefaultWidth = table.Column<int>(type: "integer", nullable: false),
                    DefaultHeight = table.Column<int>(type: "integer", nullable: false),
                    VisibleFieldsJson = table.Column<string>(type: "jsonb", nullable: false),
                    DefaultValuesJson = table.Column<string>(type: "jsonb", nullable: false),
                    PublishedByUserId = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannerTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannerTemplateVersions_PlannerTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "PlannerTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerNodes_TemplateVersionId",
                table: "PlannerNodes",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerTemplates_ObjectType_SortOrder",
                table: "PlannerTemplates",
                columns: new[] { "ObjectType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannerTemplateVersions_TemplateId_VersionNumber",
                table: "PlannerTemplateVersions",
                columns: new[] { "TemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PlannerNodes_PlannerTemplateVersions_TemplateVersionId",
                table: "PlannerNodes",
                column: "TemplateVersionId",
                principalTable: "PlannerTemplateVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlannerNodes_PlannerTemplateVersions_TemplateVersionId",
                table: "PlannerNodes");

            migrationBuilder.DropTable(
                name: "PlannerTemplateVersions");

            migrationBuilder.DropTable(
                name: "PlannerTemplates");

            migrationBuilder.DropIndex(
                name: "IX_PlannerNodes_TemplateVersionId",
                table: "PlannerNodes");

            migrationBuilder.DropColumn(
                name: "TemplateVersionId",
                table: "PlannerNodes");
        }
    }
}
