using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectJA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIssueStatusWithWorkflows : Migration
    {
        // Replaces the fixed IssueStatus enum (Todo=0/Doing=1/Done=2) with the
        // custom-workflow model: every project gets a "Default" workflow with
        // three states matching the old enum, and every existing issue is
        // pointed at the right state by the data-move block in Up().
        //
        // Order matters here — the EF scaffolder defaulted to dropping Status
        // first; that would lose every issue's status. Rewritten so the new
        // tables + column exist (nullable) BEFORE the data move, then we tighten
        // to NOT NULL and drop the old column at the end.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Drop the now-stale composite index on (ProjectId, Status); the
            //    new (ProjectId, WorkflowStateId) index gets added below.
            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId_Status",
                table: "issues");

            // 2) Create the workflow tables.
            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_states_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 3) Add WorkflowStateId on issues as NULLABLE first so the data move
            //    can populate it before we tighten to NOT NULL.
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStateId",
                table: "issues",
                type: "uuid",
                nullable: true);

            // 4) Data move: for every project, create a Default workflow with
            //    three states (Todo / Doing / Done categorised Open / InProgress
            //    / Done) and update each issue's WorkflowStateId by mapping its
            //    old Status int. gen_random_uuid() is from pgcrypto in
            //    Postgres 13+ / built-in in Postgres 16; safe for this app.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                  p RECORD;
                  wf_id uuid;
                  todo_id uuid;
                  doing_id uuid;
                  done_id uuid;
                  now_ts timestamptz := now();
                BEGIN
                  FOR p IN SELECT "Id" FROM projects LOOP
                    wf_id    := gen_random_uuid();
                    todo_id  := gen_random_uuid();
                    doing_id := gen_random_uuid();
                    done_id  := gen_random_uuid();

                    INSERT INTO workflows ("Id", "ProjectId", "Name", "IsDefault", "CreatedAt")
                      VALUES (wf_id, p."Id", 'Default', true, now_ts);

                    INSERT INTO workflow_states ("Id", "WorkflowId", "Name", "Order", "Category") VALUES
                      (todo_id,  wf_id, 'Todo',  0, 0),
                      (doing_id, wf_id, 'Doing', 1, 1),
                      (done_id,  wf_id, 'Done',  2, 2);

                    UPDATE issues
                       SET "WorkflowStateId" = CASE "Status"
                         WHEN 0 THEN todo_id
                         WHEN 1 THEN doing_id
                         WHEN 2 THEN done_id
                       END
                     WHERE "ProjectId" = p."Id";
                  END LOOP;
                END $$;
            """);

            // 5) Now safe to tighten to NOT NULL — every row has a value.
            migrationBuilder.AlterColumn<Guid>(
                name: "WorkflowStateId",
                table: "issues",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // 6) Drop the old Status column.
            migrationBuilder.DropColumn(
                name: "Status",
                table: "issues");

            // 7) Indexes on the new column + workflow tables.
            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId_WorkflowStateId",
                table: "issues",
                columns: new[] { "ProjectId", "WorkflowStateId" });

            migrationBuilder.CreateIndex(
                name: "IX_issues_WorkflowStateId",
                table: "issues",
                column: "WorkflowStateId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_states_WorkflowId",
                table: "workflow_states",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_states_WorkflowId_Order",
                table: "workflow_states",
                columns: new[] { "WorkflowId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_workflows_ProjectId",
                table: "workflows",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the data move: add Status back, populate from each
            // issue's workflow state Category (Open→0/InProgress→1/Done→2),
            // then drop the new tables/columns. Custom state names are lost
            // on downgrade — accepted; downgrade is rare and the categories
            // preserve the lifecycle position.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "issues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE issues i
                   SET "Status" = COALESCE(s."Category", 0)
                  FROM workflow_states s
                 WHERE s."Id" = i."WorkflowStateId";
            """);

            migrationBuilder.DropIndex(
                name: "IX_issues_ProjectId_WorkflowStateId",
                table: "issues");

            migrationBuilder.DropIndex(
                name: "IX_issues_WorkflowStateId",
                table: "issues");

            migrationBuilder.DropColumn(
                name: "WorkflowStateId",
                table: "issues");

            migrationBuilder.DropTable(
                name: "workflow_states");

            migrationBuilder.DropTable(
                name: "workflows");

            migrationBuilder.CreateIndex(
                name: "IX_issues_ProjectId_Status",
                table: "issues",
                columns: new[] { "ProjectId", "Status" });
        }
    }
}
