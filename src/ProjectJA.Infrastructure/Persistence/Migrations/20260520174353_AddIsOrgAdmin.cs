using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsOrgAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOrgAdmin",
                table: "aspnetusers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: every user who is currently a project Admin on at
            // least one project becomes an OrgAdmin. Captures the bootstrap
            // admin AND whoever's been running the dev tenant; newbies /
            // Members / Viewers stay False. (Role=2 is ProjectRole.Admin.)
            // Idempotent: subsequent migration replays see no change because
            // the column already exists. Admins can demote unwanted
            // promotions via /admin/users.
            migrationBuilder.Sql("""
                UPDATE aspnetusers SET "IsOrgAdmin" = true
                 WHERE "Id" IN (
                   SELECT DISTINCT "UserId"
                     FROM project_members
                    WHERE "Role" = 2
                 );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsOrgAdmin",
                table: "aspnetusers");
        }
    }
}
