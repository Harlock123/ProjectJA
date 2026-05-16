// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ProjectJA.Infrastructure.Email;

public sealed class EmailOutboxEntryConfiguration : IEntityTypeConfiguration<EmailOutboxEntry>
{
    public void Configure(EntityTypeBuilder<EmailOutboxEntry> b)
    {
        b.ToTable("email_outbox");
        b.HasKey(x => x.Id);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ScheduledFor).IsRequired();
        b.Property(x => x.Attempts).IsRequired();
        b.Property(x => x.LastAttemptAt);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.Property(x => x.SentAt);
        b.Property(x => x.DeadLetterAt);
        b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
        // Composite picker index: open entries (SentAt is null, DeadLetterAt is null) ordered by ScheduledFor.
        b.HasIndex(x => new { x.SentAt, x.DeadLetterAt, x.ScheduledFor });
    }
}
