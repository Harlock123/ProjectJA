// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class TagManagementService(DbContext db, IClock clock) : ITagManagementService
{
    public async Task<IReadOnlyList<TagSummary>> ListForProjectAsync(Guid projectId, CancellationToken ct)
    {
        // Issue.Labels is a Postgres text[] (PrimitiveCollection). EF can't
        // express "unnest + group" via LINQ, so this is the cleanest path. The
        // outer SELECT returns one row per distinct tag in the project.
        const string sql = @"
            SELECT tag AS ""Tag"", COUNT(*)::int AS ""IssueCount""
            FROM (
                SELECT unnest(""Labels"") AS tag
                FROM issues
                WHERE ""ProjectId"" = {0}
            ) t
            GROUP BY tag
            ORDER BY tag";
        var rows = await db.Database
            .SqlQueryRaw<TagSummary>(sql, projectId)
            .ToListAsync(ct);
        return rows;
    }

    public async Task<int> RenameAsync(Guid projectId, string oldTag, string newTag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldTag))
            throw new ArgumentException("Old tag is required.", nameof(oldTag));
        if (string.IsNullOrWhiteSpace(newTag))
            throw new ArgumentException("New tag is required.", nameof(newTag));

        var trimmedOld = oldTag.Trim();
        var trimmedNew = newTag.Trim();
        if (string.Equals(trimmedOld, trimmedNew, StringComparison.OrdinalIgnoreCase))
            return 0; // No-op rename; let the dialog show 0 touched.

        // Load every issue in the project that carries the old tag (exact
        // match — Postgres array @> is case-sensitive, but SetLabels stores
        // first-casing-wins so the listing query returns canonical casings).
        var hits = await LoadHitsAsync(projectId, trimmedOld, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        foreach (var issue in hits)
        {
            // Rebuild the label set: swap any case-insensitive match of the
            // old tag for the new one. SetLabels normalises (trim, cap to 40
            // chars, dedupe case-insensitively, cap to 20), so an issue that
            // already had both old and new collapses cleanly.
            var rebuilt = issue.Labels.Select(l =>
                string.Equals(l, trimmedOld, StringComparison.OrdinalIgnoreCase) ? trimmedNew : l);
            issue.SetLabels(rebuilt, now);
        }
        await db.SaveChangesAsync(ct);
        return hits.Count;
    }

    public async Task<int> RemoveAsync(Guid projectId, string tag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag is required.", nameof(tag));

        var trimmed = tag.Trim();
        var hits = await LoadHitsAsync(projectId, trimmed, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        foreach (var issue in hits)
        {
            var rebuilt = issue.Labels.Where(l =>
                !string.Equals(l, trimmed, StringComparison.OrdinalIgnoreCase));
            issue.SetLabels(rebuilt, now);
        }
        await db.SaveChangesAsync(ct);
        return hits.Count;
    }

    private async Task<List<Issue>> LoadHitsAsync(Guid projectId, string tag, CancellationToken ct)
    {
        // Translate to Postgres: issues WHERE "Labels" @> ARRAY[@tag]. For a
        // case-insensitive list we'd have to unnest+lower, but stored tags are
        // canonicalised via SetLabels so the exact match is sufficient — the
        // listing query returns whatever case is actually in the DB.
        return await db.Set<Issue>()
            .Where(i => i.ProjectId == projectId && i.Labels.Contains(tag))
            .ToListAsync(ct);
    }
}
