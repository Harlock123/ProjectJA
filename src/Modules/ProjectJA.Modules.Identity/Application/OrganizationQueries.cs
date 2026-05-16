// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Application;

internal sealed class OrganizationQueries(DbContext db) : IOrganizationQueries
{
    public async Task<OrganizationSummary?> GetDefaultAsync(CancellationToken ct)
    {
        return await db.Set<Organization>()
            .AsNoTracking()
            .OrderBy(o => o.CreatedAt)
            .Select(o => new OrganizationSummary(o.Id, o.Slug, o.Name))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OrganizationSummary?> GetByIdAsync(Guid organizationId, CancellationToken ct)
    {
        return await db.Set<Organization>()
            .AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new OrganizationSummary(o.Id, o.Slug, o.Name))
            .FirstOrDefaultAsync(ct);
    }
}
