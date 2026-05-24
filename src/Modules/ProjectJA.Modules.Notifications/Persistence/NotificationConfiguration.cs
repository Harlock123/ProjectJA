// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Notifications.Domain;

namespace ProjectJA.Modules.Notifications.Persistence;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipientUserId).IsRequired();
        b.Property(x => x.ActorId);
        b.Property(x => x.Kind).HasMaxLength(64).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Link).HasMaxLength(500).IsRequired();
        b.Property(x => x.ResourceId);
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ReadAt);

        // The inbox query is "unread for me, newest first" so the index orders
        // descending by CreatedAt within a recipient.
        b.HasIndex(x => new { x.RecipientUserId, x.CreatedAt })
            .IsDescending(false, true);
        b.HasIndex(x => new { x.RecipientUserId, x.ReadAt });
    }
}
