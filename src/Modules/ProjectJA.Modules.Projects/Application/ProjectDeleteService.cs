// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ProjectJA.Modules.Projects.Contracts;

namespace ProjectJA.Modules.Projects.Application;

internal sealed class ProjectDeleteService(DbContext db) : IProjectDeleteService
{
    // The entity FK graph between modules is modelled as loose Guid columns
    // (no EF navigations cross-module), so EF cascade can't help. Raw SQL is
    // the clearest way to express the delete tree and is dramatically faster
    // than loading thousands of rows through the change tracker.
    private static readonly string[] OrderedDeletes =
    [
        // Anything that points at issues — must go first.
        @"DELETE FROM issue_links
            WHERE ""BlockerIssueId"" IN (SELECT ""Id"" FROM issues WHERE ""ProjectId"" = {0})
               OR ""BlockedIssueId"" IN (SELECT ""Id"" FROM issues WHERE ""ProjectId"" = {0})",
        @"DELETE FROM issue_attachments
            WHERE ""IssueId"" IN (SELECT ""Id"" FROM issues WHERE ""ProjectId"" = {0})",
        @"DELETE FROM issue_comments
            WHERE ""IssueId"" IN (SELECT ""Id"" FROM issues WHERE ""ProjectId"" = {0})",
        // Now the issues themselves.
        @"DELETE FROM issues WHERE ""ProjectId"" = {0}",
        // Workflow tree (transitions → states → workflow).
        @"DELETE FROM workflow_transitions
            WHERE ""WorkflowId"" IN (SELECT ""Id"" FROM workflows WHERE ""ProjectId"" = {0})",
        @"DELETE FROM workflow_states
            WHERE ""WorkflowId"" IN (SELECT ""Id"" FROM workflows WHERE ""ProjectId"" = {0})",
        @"DELETE FROM workflows WHERE ""ProjectId"" = {0}",
        // Sprints + members (members table is owned by project; deleting the
        // project row would handle it on its own, but doing it explicitly here
        // keeps the wipe atomic if the final project-row delete is rolled back).
        @"DELETE FROM sprints WHERE ""ProjectId"" = {0}",
        @"DELETE FROM project_members WHERE ""ProjectId"" = {0}",
        @"DELETE FROM projects WHERE ""Id"" = {0}",
    ];

    public async Task<bool> DeleteAsync(Guid projectId, CancellationToken ct)
    {
        // Single transaction so a mid-delete failure leaves the project intact
        // instead of half-shredded. Re-uses the ambient context's connection.
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        var lastRows = 0;
        foreach (var sql in OrderedDeletes)
            lastRows = await db.Database.ExecuteSqlRawAsync(sql, new object[] { projectId }, ct);
        // lastRows here is the count from the final DELETE (projects) — 1 if
        // the project existed, 0 otherwise. We can't trust earlier counts (an
        // empty issue list is fine).
        await tx.CommitAsync(ct);
        return lastRows > 0;
    }
}
