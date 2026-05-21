// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class IssueLinkService(DbContext db, IClock clock) : IIssueLinkService
{
    public async Task<IssueLinksView> GetLinksAsync(Guid issueId, CancellationToken ct)
    {
        // Order on the entity (Issue.Number) BEFORE projecting to the record.
        // EF can't translate OrderBy over a freshly-constructed record member.
        var blockedBy = await db.Set<IssueLink>().AsNoTracking()
            .Where(l => l.BlockedIssueId == issueId)
            .Join(db.Set<Issue>().AsNoTracking(), l => l.BlockerIssueId, i => i.Id,
                (l, i) => i)
            .OrderBy(i => i.Number)
            .Select(i => new IssueLinkSummary(i.Id, i.Number, i.Title))
            .ToListAsync(ct);
        var blocks = await db.Set<IssueLink>().AsNoTracking()
            .Where(l => l.BlockerIssueId == issueId)
            .Join(db.Set<Issue>().AsNoTracking(), l => l.BlockedIssueId, i => i.Id,
                (l, i) => i)
            .OrderBy(i => i.Number)
            .Select(i => new IssueLinkSummary(i.Id, i.Number, i.Title))
            .ToListAsync(ct);
        return new IssueLinksView(blockedBy, blocks);
    }

    public async Task<AddBlockerOutcome> AddBlockerAsync(Guid blockedIssueId, Guid blockerIssueId, Guid actingUserId, CancellationToken ct)
    {
        if (blockerIssueId == blockedIssueId) return AddBlockerOutcome.SelfLink;

        var blocked = await db.Set<Issue>().AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == blockedIssueId, ct);
        if (blocked is null) return AddBlockerOutcome.NotFound;
        var blocker = await db.Set<Issue>().AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == blockerIssueId, ct);
        if (blocker is null) return AddBlockerOutcome.NotFound;
        if (blocker.ProjectId != blocked.ProjectId) return AddBlockerOutcome.CrossProject;

        var exists = await db.Set<IssueLink>().AsNoTracking()
            .AnyAsync(l => l.BlockerIssueId == blockerIssueId && l.BlockedIssueId == blockedIssueId, ct);
        if (exists) return AddBlockerOutcome.Duplicate;

        // Cycle check: forward BFS from `blocked` over the blocks graph. If we
        // can reach `blocker`, the new edge would close a loop. Graph is
        // tenant-scoped and small; load once and walk in memory.
        var allLinks = await db.Set<IssueLink>().AsNoTracking()
            .Select(l => new { l.BlockerIssueId, l.BlockedIssueId })
            .ToListAsync(ct);
        var adjacency = allLinks
            .GroupBy(l => l.BlockerIssueId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.BlockedIssueId).ToList());
        var visited = new HashSet<Guid> { blockedIssueId };
        var queue = new Queue<Guid>();
        queue.Enqueue(blockedIssueId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var nexts)) continue;
            foreach (var n in nexts)
            {
                if (n == blockerIssueId) return AddBlockerOutcome.Cycle;
                if (visited.Add(n)) queue.Enqueue(n);
            }
        }

        var link = IssueLink.Create(blockerIssueId, blockedIssueId, actingUserId, clock.UtcNow);
        db.Set<IssueLink>().Add(link);
        await db.SaveChangesAsync(ct);
        return AddBlockerOutcome.Added;
    }

    public async Task<bool> RemoveBlockerAsync(Guid blockedIssueId, Guid blockerIssueId, CancellationToken ct)
    {
        var link = await db.Set<IssueLink>()
            .FirstOrDefaultAsync(l => l.BlockedIssueId == blockedIssueId && l.BlockerIssueId == blockerIssueId, ct);
        if (link is null) return false;
        db.Set<IssueLink>().Remove(link);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
