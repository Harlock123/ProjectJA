using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssueClassificationAndPeople : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcceptanceCriteria",
                table: "issues",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "issues",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReporterId",
                table: "issues",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId_Type",
                table: "issues",
                columns: new[] { "ProjectId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId_Type",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteria",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Points",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "ReporterId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "issues");
        }
    }
}
