// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Persistence;

/// <summary>
/// Creates short-lived <see cref="AppDbContext"/> instances bound to the current
/// tenant's connection. For callers that need an isolated change tracker instead
/// of the shared circuit-scoped context — e.g. interactive Blazor components that
/// write while realtime handlers reload on the same circuit. Mirrors the per-scope
/// construction in DependencyInjection so the tenant routing is identical.
/// </summary>
public sealed class TenantAppDbContextFactory(ITenantContext tenant) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(tenant.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }
}
