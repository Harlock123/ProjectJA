using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueScheduleProgressAndLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndDate",
                table: "issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PercentComplete",
                table: "issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StartDate",
                table: "issues",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "issue_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockerIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedIssueId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_issue_links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_BlockedIssueId",
                table: "issue_links",
                column: "BlockedIssueId");

            migrationBuilder.CreateIndex(
                name: "IX_issue_links_BlockerIssueId_BlockedIssueId",
                table: "issue_links",
                columns: new[] { "BlockerIssueId", "BlockedIssueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_links");

            migrationBuilder.DropColumn(
                name: "EndDate",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "PercentComplete",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "issues");
        }
    }
}
