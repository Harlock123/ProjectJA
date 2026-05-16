// SPDX-License-Identifier: BUSL-1.1
using Coravel.Invocable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectJA.Modules.Audit.Domain;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Audit.Application;

/// <summary>
/// Coravel daily job. Iterates every tenant in the directory and bulk-deletes
/// <see cref="AuditEvent"/> rows older than <see cref="AuditOptions.RetentionDays"/>.
/// </summary>
public sealed class AuditRetentionJob(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditOptions> options,
    ILogger<AuditRetentionJob> logger) : IInvocable
{
    public async Task Invoke()
    {
        var retentionDays = Math.Max(1, options.Value.RetentionDays);

        using var rootScope = scopeFactory.CreateScope();
        var directory = rootScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var clock = rootScope.ServiceProvider.GetRequiredService<IClock>();
        var cutoff = clock.UtcNow.AddDays(-retentionDays);

        var tenants = await directory.ListAllAsync(CancellationToken.None).ConfigureAwait(false);

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                scope.ServiceProvider.UseTenant(tenant);

                var db = scope.ServiceProvider.GetRequiredService<DbContext>();
                var deleted = await db.Set<AuditEvent>()
                    .Where(e => e.OccurredAt < cutoff)
                    .ExecuteDeleteAsync()
                    .ConfigureAwait(false);

                if (deleted > 0)
                    logger.LogInformation(
                        "Audit retention: tenant {Tenant} purged {Count} events older than {Cutoff:u}.",
                        tenant.Slug, deleted, cutoff);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audit retention purge failed for tenant {Tenant}", tenant.Slug);
            }
        }
    }
}
