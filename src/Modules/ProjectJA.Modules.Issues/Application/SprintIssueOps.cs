// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class SprintIssueOps(DbContext db) : ISprintIssueOps
{
    public async Task<int> ClearSprintForIncompleteAsync(Guid sprintId, DateTimeOffset now, CancellationToken ct)
    {
        // "Incomplete" = workflow state's Category is not Done. We don't load
        // the workflow per-issue — instead pull the set of Done-category state
        // ids in one query, then filter. Tracked update (not ExecuteUpdateAsync)
        // so the domain method runs and UpdatedAt advances.
        var doneStateIds = await db.Set<Workflow>().AsNoTracking()
            .SelectMany(w => w.States)
            .Where(s => s.Category == WorkflowStateCategory.Done)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var incomplete = await db.Set<Issue>()
            .Where(i => i.SprintId == sprintId && !doneStateIds.Contains(i.WorkflowStateId))
            .ToListAsync(ct);
        foreach (var issue in incomplete)
            issue.AssignToSprint(null, now);
        if (incomplete.Count > 0)
            await db.SaveChangesAsync(ct);
        return incomplete.Count;
    }

    public Task<bool> HasAnyIssuesInSprintAsync(Guid sprintId, CancellationToken ct) =>
        db.Set<Issue>().AsNoTracking().AnyAsync(i => i.SprintId == sprintId, ct);
}
