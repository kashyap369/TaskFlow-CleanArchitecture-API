using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Infra.Migrations
{
    /// <summary>
    /// Adds the first-run onboarding marker, and closes it out for every
    /// account that already exists.
    ///
    /// The backfill is the point of this migration, not a convenience: the
    /// welcome used to be remembered in the browser, so anyone who cleared
    /// site data or signed in from a second machine saw it again. Stamping
    /// existing rows is what makes the server-side flag mean "has never
    /// been shown" rather than "has not been shown in this browser", and
    /// it is what keeps the welcome to genuinely new registrations.
    /// </summary>
    public partial class AddUserOnboardingCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OnboardingCompletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            // Every account that exists at this point predates the flag and
            // has already had its chance to see the welcome. Soft-deleted
            // rows are stamped too — they may yet be restored.
            migrationBuilder.Sql(
                """
                UPDATE "Users"
                SET "OnboardingCompletedAt" = COALESCE("LastLoginAt", "CreatedAt", NOW())
                WHERE "OnboardingCompletedAt" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "Users");
        }
    }
}
