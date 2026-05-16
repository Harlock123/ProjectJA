// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Infrastructure.Email;

internal sealed class EmailOutboxAdmin(DbContext db, IClock clock, IAuditLog audit) : IEmailOutboxAdmin
{
    public async Task<IReadOnlyList<EmailOutboxEntryView>> ListAsync(
        EmailOutboxFilter filter, int limit, CancellationToken ct)
    {
        var cap = Math.Clamp(limit, 1, 500);
        var q = db.Set<EmailOutboxEntry>().AsNoTracking();
        q = filter switch
        {
            EmailOutboxFilter.Pending => q.Where(e => e.SentAt == null && e.DeadLetterAt == null),
            EmailOutboxFilter.DeadLettered => q.Where(e => e.DeadLetterAt != null),
            EmailOutboxFilter.Sent => q.Where(e => e.SentAt != null),
            EmailOutboxFilter.All => q,
            _ => q,
        };

        var rows = await q.OrderByDescending(e => e.CreatedAt).Take(cap).ToListAsync(ct);
        return rows.Select(ToView).ToList();
    }

    public async Task<bool> RetryAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.Set<EmailOutboxEntry>().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return false;

        entry.DeadLetterAt = null;
        entry.SentAt = null;
        entry.Attempts = 0;
        entry.LastError = null;
        entry.LastAttemptAt = null;
        entry.ScheduledFor = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "email.retry",
            ResourceType: "EmailOutboxEntry",
            ResourceId: id.ToString(),
            Summary: "Email retried from outbox"), ct);
        return true;
    }

    public async Task<bool> DiscardAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.Set<EmailOutboxEntry>().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entry is null) return false;

        db.Set<EmailOutboxEntry>().Remove(entry);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "email.discard",
            ResourceType: "EmailOutboxEntry",
            ResourceId: id.ToString(),
            Summary: "Email discarded from outbox"), ct);
        return true;
    }

    private static EmailOutboxEntryView ToView(EmailOutboxEntry e)
    {
        string? subject = null;
        string? to = null;

        // Payload is a serialized EmailMessage record (PascalCase). Parse just enough
        // for display without rehydrating the full message.
        try
        {
            using var doc = JsonDocument.Parse(e.Payload);
            var root = doc.RootElement;
            if (root.TryGetProperty("Subject", out var subj) && subj.ValueKind == JsonValueKind.String)
                subject = subj.GetString();
            if (root.TryGetProperty("To", out var toArray) && toArray.ValueKind == JsonValueKind.Array)
            {
                var addresses = new List<string>();
                foreach (var item in toArray.EnumerateArray())
                {
                    if (item.TryGetProperty("Address", out var addr) && addr.GetString() is { } s)
                        addresses.Add(s);
                }
                to = addresses.Count > 0 ? string.Join(", ", addresses) : null;
            }
        }
        catch { /* leave subject/to null on malformed payload */ }

        return new EmailOutboxEntryView(
            e.Id, e.CreatedAt, e.ScheduledFor, e.Attempts, e.LastAttemptAt, e.LastError,
            e.SentAt, e.DeadLetterAt, subject, to);
    }
}
