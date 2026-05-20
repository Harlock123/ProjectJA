// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class SprintIssueOps(DbContext db) : ISprintIssueOps
{
    public async Task<int> ClearSprintForIncompleteAsync(Guid sprintId, DateTimeOffset now, CancellationToken ct)
    {
        // Tracked update (not ExecuteUpdateAsync) so the domain method runs and
        // UpdatedAt advances — keeping audit/UI freshness consistent. The set
        // is bounded by sprint size, so the per-row cost is fine.
        var incomplete = await db.Set<Issue>()
            .Where(i => i.SprintId == sprintId && i.Status != IssueStatus.Done)
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
