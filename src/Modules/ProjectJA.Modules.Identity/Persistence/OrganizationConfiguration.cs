// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Persistence;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> b)
    {
        b.ToTable("organizations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Slug).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => x.Slug).IsUnique();
    }
}
