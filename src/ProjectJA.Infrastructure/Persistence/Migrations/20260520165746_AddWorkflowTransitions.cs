using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowTransitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_transitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_WorkflowId_FromStateId_ToStateId",
                table: "workflow_transitions",
                columns: new[] { "WorkflowId", "FromStateId", "ToStateId" },
                unique: true);

            // Data move: for every existing workflow, seed every directional
            // pair of its states (skip self-pairs). Preserves the pre-this-
            // slice any-to-any behavior so nothing breaks for users that
            // haven't customized transitions yet.
            migrationBuilder.Sql("""
                INSERT INTO workflow_transitions ("Id", "WorkflowId", "FromStateId", "ToStateId")
                SELECT gen_random_uuid(), a."WorkflowId", a."Id", b."Id"
                  FROM workflow_states a
                  JOIN workflow_states b
                    ON a."WorkflowId" = b."WorkflowId"
                   AND a."Id" <> b."Id";
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_transitions");
        }
    }
}
