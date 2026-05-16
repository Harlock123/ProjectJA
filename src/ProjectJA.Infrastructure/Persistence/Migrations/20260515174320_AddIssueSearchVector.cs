using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueSearchVector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "issues",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('english', coalesce(\"Title\", '')), 'A') || setweight(to_tsvector('english', coalesce(\"Description\", '')), 'B')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_issues_SearchVector",
                table: "issues",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_issues_SearchVector",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "issues");
        }
    }
}
