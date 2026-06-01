// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Domain;
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

        // Move the persisted style (if any) onto the new name so the chip
        // keeps its colour after the rename. If a style already exists for
        // the new name, the old one wins (overwrite) — matches the labels-
        // collapse direction above.
        var styles = await db.Set<ProjectTagStyle>()
            .Where(s => s.ProjectId == projectId
                        && (s.Tag == trimmedOld || s.Tag == trimmedNew))
            .ToListAsync(ct);
        var oldStyle = styles.FirstOrDefault(s => string.Equals(s.Tag, trimmedOld, StringComparison.OrdinalIgnoreCase));
        var newStyle = styles.FirstOrDefault(s => string.Equals(s.Tag, trimmedNew, StringComparison.OrdinalIgnoreCase));
        if (oldStyle is not null)
        {
            if (newStyle is not null) db.Set<ProjectTagStyle>().Remove(newStyle);
            oldStyle.Rename(trimmedNew, now);
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

        // Drop the style row too — keeping it would orphan the colour for a
        // tag that no longer exists on any issue. If the user re-adds the tag
        // later they'll start from the default look.
        var style = await db.Set<ProjectTagStyle>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Tag == trimmed, ct);
        if (style is not null) db.Set<ProjectTagStyle>().Remove(style);

        await db.SaveChangesAsync(ct);
        return hits.Count;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetStylesAsync(Guid projectId, CancellationToken ct)
    {
        var rows = await db.Set<ProjectTagStyle>()
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .Select(s => new { s.Tag, s.BackgroundHex })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Tag, r => r.BackgroundHex, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetStyleAsync(Guid projectId, string tag, string backgroundHex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag is required.", nameof(tag));
        if (string.IsNullOrWhiteSpace(backgroundHex))
        {
            await ClearStyleAsync(projectId, tag, ct);
            return;
        }

        var trimmedTag = tag.Trim();
        var hex = NormalizeHex(backgroundHex)
            ?? throw new ArgumentException($"'{backgroundHex}' isn't a #RRGGBB or #RRGGBBAA value.", nameof(backgroundHex));

        var now = clock.UtcNow;
        var existing = await db.Set<ProjectTagStyle>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Tag == trimmedTag, ct);
        if (existing is null)
        {
            db.Set<ProjectTagStyle>().Add(ProjectTagStyle.Create(projectId, trimmedTag, hex, now));
        }
        else
        {
            existing.SetBackground(hex, now);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearStyleAsync(Guid projectId, string tag, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        var trimmed = tag.Trim();
        var style = await db.Set<ProjectTagStyle>()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Tag == trimmed, ct);
        if (style is null) return;
        db.Set<ProjectTagStyle>().Remove(style);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> CopyStyleAsync(Guid projectId, string sourceTag, IEnumerable<string> targetTags, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceTag))
            throw new ArgumentException("Source tag is required.", nameof(sourceTag));

        var sourceTrimmed = sourceTag.Trim();
        var sourceStyle = await db.Set<ProjectTagStyle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Tag == sourceTrimmed, ct)
            ?? throw new InvalidOperationException($"Source tag '{sourceTrimmed}' has no stored style to copy.");

        // Dedupe targets case-insensitively and drop the source itself —
        // copying onto yourself is a no-op and would falsely inflate the count.
        var targets = (targetTags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Where(t => !string.Equals(t, sourceTrimmed, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (targets.Count == 0) return 0;

        // One round-trip to load whichever targets already have styles; the
        // rest are inserts. Save once at the end so the whole copy is atomic
        // (transaction semantics inherited from the AppDbContext save).
        var existing = await db.Set<ProjectTagStyle>()
            .Where(s => s.ProjectId == projectId && targets.Contains(s.Tag))
            .ToListAsync(ct);
        var existingByTag = existing.ToDictionary(s => s.Tag, StringComparer.OrdinalIgnoreCase);

        var now = clock.UtcNow;
        foreach (var t in targets)
        {
            if (existingByTag.TryGetValue(t, out var row))
                row.SetBackground(sourceStyle.BackgroundHex, now);
            else
                db.Set<ProjectTagStyle>().Add(ProjectTagStyle.Create(projectId, t, sourceStyle.BackgroundHex, now));
        }
        await db.SaveChangesAsync(ct);
        return targets.Count;
    }

    public async Task<int> RippleAsync(Guid projectId, string sourceTag, string newBackgroundHex, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceTag))
            throw new ArgumentException("Source tag is required.", nameof(sourceTag));
        var newHex = NormalizeHex(newBackgroundHex)
            ?? throw new ArgumentException($"'{newBackgroundHex}' isn't a #RRGGBB or #RRGGBBAA value.", nameof(newBackgroundHex));

        var trimmedSource = sourceTag.Trim();
        var sourceStyle = await db.Set<ProjectTagStyle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.Tag == trimmedSource, ct)
            ?? throw new InvalidOperationException(
                $"Tag '{trimmedSource}' has no current style — nothing to ripple from.");

        // The "group" = every tag currently sharing the source's colour
        // (case-insensitive on the hex, since we store lowercased). Includes
        // the source itself.
        var oldHex = sourceStyle.BackgroundHex;
        var group = await db.Set<ProjectTagStyle>()
            .Where(s => s.ProjectId == projectId
                        && s.BackgroundHex.ToLower() == oldHex.ToLower())
            .ToListAsync(ct);

        // Short-circuit if the user picked the same colour the group already
        // has — nothing to write, but report the group size so the dialog can
        // still show "applied to N tags".
        if (string.Equals(newHex, oldHex, StringComparison.OrdinalIgnoreCase))
            return group.Count;

        var now = clock.UtcNow;
        foreach (var row in group)
            row.SetBackground(newHex, now);
        await db.SaveChangesAsync(ct);
        return group.Count;
    }

    private static string? NormalizeHex(string raw)
    {
        var s = raw.Trim();
        if (s.Length == 0) return null;
        if (s[0] != '#') s = "#" + s;
        // Allow #RGB, #RRGGBB, #RRGGBBAA. Expand short form to long.
        if (s.Length == 4 && IsHexBody(s, 1))
            return $"#{s[1]}{s[1]}{s[2]}{s[2]}{s[3]}{s[3]}".ToLowerInvariant();
        if ((s.Length == 7 || s.Length == 9) && IsHexBody(s, 1))
            return s.ToLowerInvariant();
        return null;
    }

    private static bool IsHexBody(string s, int from)
    {
        for (var i = from; i < s.Length; i++)
            if (!Uri.IsHexDigit(s[i])) return false;
        return true;
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
