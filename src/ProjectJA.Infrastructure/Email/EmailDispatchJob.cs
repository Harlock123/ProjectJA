// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Coravel.Invocable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectJA.Infrastructure.Tenancy;
using ProjectJA.SharedKernel.Email;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Infrastructure.Email;

/// <summary>
/// Coravel scheduled job. Iterates every tenant in the directory and dispatches
/// pending <see cref="EmailOutboxEntry"/> rows via <see cref="IEmailDispatcher"/>
/// with exponential backoff. Entries that fail past <see cref="MaxAttempts"/>
/// are dead-lettered (kept in the table with <c>DeadLetterAt</c> set).
/// </summary>
public sealed class EmailDispatchJob(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailDispatchJob> logger) : IInvocable
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 6;

    public async Task Invoke()
    {
        using var rootScope = scopeFactory.CreateScope();
        var directory = rootScope.ServiceProvider.GetRequiredService<ITenantDirectory>();
        var tenants = await directory.ListAllAsync(CancellationToken.None).ConfigureAwait(false);

        foreach (var tenant in tenants)
        {
            try
            {
                using var tenantScope = scopeFactory.CreateScope();
                tenantScope.ServiceProvider.UseTenant(tenant);
                await ProcessTenantAsync(tenantScope.ServiceProvider).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email dispatch failed for tenant {Tenant}", tenant.Slug);
            }
        }
    }

    private async Task ProcessTenantAsync(IServiceProvider scoped)
    {
        var db = scoped.GetRequiredService<DbContext>();
        var dispatcher = scoped.GetRequiredService<IEmailDispatcher>();
        var clock = scoped.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var pending = await db.Set<EmailOutboxEntry>()
            .Where(e => e.SentAt == null
                     && e.DeadLetterAt == null
                     && e.ScheduledFor <= now)
            .OrderBy(e => e.ScheduledFor)
            .Take(BatchSize)
            .ToListAsync().ConfigureAwait(false);

        foreach (var entry in pending)
        {
            try
            {
                var message = JsonSerializer.Deserialize<EmailMessage>(entry.Payload, QueuedEmailSender.SerializerOptions)
                    ?? throw new InvalidOperationException("Outbox entry payload deserialized to null.");
                await dispatcher.DispatchAsync(message, CancellationToken.None).ConfigureAwait(false);
                entry.SentAt = clock.UtcNow;
                entry.LastError = null;
            }
            catch (Exception ex)
            {
                entry.Attempts++;
                entry.LastAttemptAt = clock.UtcNow;
                entry.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;

                if (entry.Attempts >= MaxAttempts)
                {
                    entry.DeadLetterAt = clock.UtcNow;
                    logger.LogWarning(ex, "Email {EntryId} dead-lettered after {Attempts} attempts.", entry.Id, entry.Attempts);
                }
                else
                {
                    // Exponential backoff: 30s, 1m, 2m, 4m, 8m... capped at 30 minutes.
                    var delay = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, entry.Attempts - 1), 30 * 60));
                    entry.ScheduledFor = clock.UtcNow.Add(delay);
                }
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync().ConfigureAwait(false);
    }
}
