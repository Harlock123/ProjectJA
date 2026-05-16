// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Persistence;

public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    public void Configure(EntityTypeBuilder<Invite> b)
    {
        b.ToTable("invites");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrganizationId).IsRequired();
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedById).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.ExpiresAt).IsRequired();
        b.Property(x => x.AcceptedAt);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.OrganizationId, x.Email, x.AcceptedAt });
    }
}
