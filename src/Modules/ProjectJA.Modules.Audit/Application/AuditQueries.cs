// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Audit.Contracts;
using ProjectJA.Modules.Audit.Domain;

namespace ProjectJA.Modules.Audit.Application;

internal sealed class AuditQueries(DbContext db) : IAuditQueries
{
    public async Task<IReadOnlyList<AuditEntryView>> ListRecentAsync(int limit, CancellationToken ct)
    {
        var cap = Math.Clamp(limit, 1, 500);
        return await db.Set<AuditEvent>()
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Take(cap)
            .Select(e => new AuditEntryView(
                e.Id, e.ActorId, e.OccurredAt, e.Action, e.ResourceType,
                e.ResourceId, e.Summary, e.IpAddress))
            .ToListAsync(ct);
    }
}
