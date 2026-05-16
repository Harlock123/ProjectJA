// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Application;

internal sealed class UserQueries(DbContext db) : IUserQueries
{
    public async Task<UserSummary?> GetByIdAsync(Guid userId, CancellationToken ct)
    {
        return await db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserSummary(u.Id, u.Email!, BuildDisplayName(u)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, UserSummary>> GetSummariesAsync(
        IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, UserSummary>();

        var rows = await db.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new UserSummary(u.Id, u.Email!, BuildDisplayName(u)))
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id);
    }

    private static string BuildDisplayName(ApplicationUser u)
    {
        var name = ($"{u.FirstName} {u.LastName}").Trim();
        return string.IsNullOrWhiteSpace(name) ? (u.Email ?? u.UserName ?? "(unknown)") : name;
    }
}
