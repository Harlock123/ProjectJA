// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Infrastructure.Email;

public sealed class EmailOutboxEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ScheduledFor { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeadLetterAt { get; set; }

    /// <summary>JSON-serialized <see cref="SharedKernel.Email.EmailMessage"/>.</summary>
    public string Payload { get; set; } = default!;
}
