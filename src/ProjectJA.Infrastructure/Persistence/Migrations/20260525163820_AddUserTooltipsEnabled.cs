using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTooltipsEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Helpful tooltips default ON for everyone — both existing rows
            // (via the column default) and new rows (the C# property default
            // is also true). Users can opt out from Settings → Appearance.
            migrationBuilder.AddColumn<bool>(
                name: "TooltipsEnabled",
                table: "aspnetusers",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TooltipsEnabled",
                table: "aspnetusers");
        }
    }
}
