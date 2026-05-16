// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectJA.SharedKernel.Email;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Infrastructure.Email;

/// <summary>
/// Persists outbound mail to the tenant's <c>email_outbox</c> table. The
/// <see cref="EmailDispatchJob"/> picks pending entries up and calls
/// <see cref="IEmailDispatcher"/> with backoff + dead-letter behavior.
/// </summary>
internal sealed class QueuedEmailSender(DbContext db, IClock clock) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var entry = new EmailOutboxEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = clock.UtcNow,
            ScheduledFor = clock.UtcNow,
            Attempts = 0,
            Payload = JsonSerializer.Serialize(message, SerializerOptions),
        };
        db.Set<EmailOutboxEntry>().Add(entry);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };
}
