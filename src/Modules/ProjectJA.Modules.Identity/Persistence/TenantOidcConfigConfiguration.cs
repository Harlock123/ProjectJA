// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Identity.Domain;

namespace ProjectJA.Modules.Identity.Persistence;

public sealed class TenantOidcConfigConfiguration : IEntityTypeConfiguration<TenantOidcConfig>
{
    public void Configure(EntityTypeBuilder<TenantOidcConfig> b)
    {
        b.ToTable("tenant_oidc_config");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Enabled).IsRequired();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Authority).HasMaxLength(500);
        b.Property(x => x.ClientId).HasMaxLength(200);
        b.Property(x => x.ClientSecret).HasMaxLength(500);
        b.Property(x => x.AutoProvision).IsRequired();
        b.Property(x => x.AutoProvisionDomainsJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
    }
}
