// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class TenantDirectory(TenantDirectoryDbContext db) : ITenantDirectory
{
    public async Task<TenantInfo?> FindBySlugAsync(string slug, CancellationToken ct)
    {
        return await db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Slug, t.Name, t.ConnectionString))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TenantInfo?> GetByIdAsync(TenantId id, CancellationToken ct)
    {
        var guid = id.Value;
        return await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == guid)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Slug, t.Name, t.ConnectionString))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TenantInfo?> GetDefaultAsync(CancellationToken ct)
    {
        return await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.CreatedAt)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Slug, t.Name, t.ConnectionString))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantInfo>> ListAllAsync(CancellationToken ct)
    {
        return await db.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Slug)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Slug, t.Name, t.ConnectionString))
            .ToListAsync(ct);
    }
}
