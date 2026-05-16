// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Audit.Domain;

namespace ProjectJA.Modules.Audit.Persistence;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> b)
    {
        b.ToTable("audit_events");
        b.HasKey(x => x.Id);
        b.Property(x => x.TenantId).IsRequired();
        b.Property(x => x.ActorId);
        b.Property(x => x.OccurredAt).IsRequired();
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.ResourceType).HasMaxLength(80).IsRequired();
        b.Property(x => x.ResourceId).HasMaxLength(128).IsRequired();
        b.Property(x => x.Summary).HasMaxLength(400).IsRequired();
        b.Property(x => x.Detail).HasColumnType("jsonb");
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.UserAgent).HasMaxLength(400);
        b.HasIndex(x => x.OccurredAt);
        b.HasIndex(x => new { x.ResourceType, x.ResourceId });
        b.HasIndex(x => x.ActorId);
    }
}
