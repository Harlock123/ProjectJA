// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Infrastructure.Email;

public enum EmailOutboxFilter
{
    Pending,
    DeadLettered,
    Sent,
    All,
}

public sealed record EmailOutboxEntryView(
    Guid Id,
    DateTimeOffset CreatedAt,
    DateTimeOffset ScheduledFor,
    int Attempts,
    DateTimeOffset? LastAttemptAt,
    string? LastError,
    DateTimeOffset? SentAt,
    DateTimeOffset? DeadLetterAt,
    string? Subject,
    string? ToAddresses);

public interface IEmailOutboxAdmin
{
    Task<IReadOnlyList<EmailOutboxEntryView>> ListAsync(EmailOutboxFilter filter, int limit, CancellationToken ct);
    Task<bool> RetryAsync(Guid id, CancellationToken ct);
    Task<bool> DiscardAsync(Guid id, CancellationToken ct);
}
