// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Application;

internal sealed class SprintQueries(DbContext db) : ISprintQueries
{
    public async Task<IReadOnlyList<SprintSummary>> ListForProjectAsync(Guid projectId, CancellationToken ct)
    {
        // Active first, then Planned (newest first), then Completed (newest first)
        // — the natural ordering for the Sprints page.
        return await db.Set<Sprint>().AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Status == SprintStatus.Active ? 0 : s.Status == SprintStatus.Planned ? 1 : 2)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new SprintSummary(
                s.Id, s.ProjectId, s.Name, s.Goal, s.Status,
                s.PlannedStart, s.PlannedEnd, s.StartedAt, s.CompletedAt, s.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<SprintSummary?> GetByIdAsync(Guid sprintId, CancellationToken ct)
    {
        return await db.Set<Sprint>().AsNoTracking()
            .Where(s => s.Id == sprintId)
            .Select(s => new SprintSummary(
                s.Id, s.ProjectId, s.Name, s.Goal, s.Status,
                s.PlannedStart, s.PlannedEnd, s.StartedAt, s.CompletedAt, s.CreatedAt))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> HasActiveSprintAsync(Guid projectId, CancellationToken ct) =>
        db.Set<Sprint>().AsNoTracking()
            .AnyAsync(s => s.ProjectId == projectId && s.Status == SprintStatus.Active, ct);
}
