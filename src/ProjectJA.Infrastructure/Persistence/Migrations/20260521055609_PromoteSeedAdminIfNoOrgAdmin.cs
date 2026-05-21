using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PromoteSeedAdminIfNoOrgAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Self-heal for tenants where AddIsOrgAdmin ran on a DB that had a
            // seeded admin but no projects yet: that backfill keyed off
            // project_members.Role=Admin, so it left nobody as OrgAdmin and the
            // bootstrap admin couldn't reach /invites etc. If the org still
            // has no OrgAdmin, promote the earliest-created user (the seeded
            // admin in fresh installs). No-op when an OrgAdmin already exists,
            // and no-op on brand-new DBs because seeding runs after migrations.
            migrationBuilder.Sql("""
                UPDATE aspnetusers
                   SET "IsOrgAdmin" = true
                 WHERE "Id" = (
                     SELECT "Id" FROM aspnetusers
                      ORDER BY "CreatedAt" ASC, "Id" ASC
                      LIMIT 1
                 )
                 AND NOT EXISTS (
                     SELECT 1 FROM aspnetusers WHERE "IsOrgAdmin" = true
                 );
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-reversible: we can't tell which OrgAdmin flag this migration
            // set vs. ones set by users or the prior backfill. Down is a no-op.
        }
    }
}
