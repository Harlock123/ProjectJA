using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantOidcConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_oidc_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Authority = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClientSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AutoProvision = table.Column<bool>(type: "boolean", nullable: false),
                    AutoProvisionDomainsJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_oidc_config", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_oidc_config");
        }
    }
}
