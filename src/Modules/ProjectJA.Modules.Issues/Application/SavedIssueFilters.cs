// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class SavedIssueFiltersService(DbContext db, IClock clock) : ISavedIssueFilters
{
    public async Task<IReadOnlyList<SavedFilterSummary>> ListAsync(Guid userId, Guid projectId, CancellationToken ct)
    {
        var rows = await db.Set<SavedIssueFilter>().AsNoTracking()
            .Where(s => s.UserId == userId && s.ProjectId == projectId)
            .OrderBy(s => s.Name)
            .Select(s => new SavedFilterSummary(s.Id, s.Name, s.FilterJson, s.UpdatedAt))
            .ToListAsync(ct);
        return rows;
    }

    public async Task<Guid> SaveAsync(Guid userId, Guid projectId, string name, string filterJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Filter name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(filterJson))
            throw new ArgumentException("Filter JSON is required.", nameof(filterJson));
        var trimmed = name.Trim();
        if (trimmed.Length > 80) trimmed = trimmed[..80];

        var now = clock.UtcNow;
        var existing = await db.Set<SavedIssueFilter>()
            .FirstOrDefaultAsync(s => s.UserId == userId
                                      && s.ProjectId == projectId
                                      && s.Name == trimmed, ct);
        if (existing is null)
        {
            var row = SavedIssueFilter.Create(userId, projectId, trimmed, filterJson, now);
            db.Set<SavedIssueFilter>().Add(row);
            await db.SaveChangesAsync(ct);
            return row.Id;
        }
        existing.Replace(filterJson, now);
        await db.SaveChangesAsync(ct);
        return existing.Id;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid filterId, CancellationToken ct)
    {
        // userId guard prevents one user from deleting another's filter even
        // if the id leaks somehow — defense-in-depth alongside the auth gate
        // on the page that surfaces the IDs.
        var row = await db.Set<SavedIssueFilter>()
            .FirstOrDefaultAsync(s => s.Id == filterId && s.UserId == userId, ct);
        if (row is null) return false;
        db.Set<SavedIssueFilter>().Remove(row);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
