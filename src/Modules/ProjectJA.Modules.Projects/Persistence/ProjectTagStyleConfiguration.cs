// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Persistence;

public sealed class ProjectTagStyleConfiguration : IEntityTypeConfiguration<ProjectTagStyle>
{
    public void Configure(EntityTypeBuilder<ProjectTagStyle> b)
    {
        b.ToTable("project_tag_styles");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Tag).HasMaxLength(40).IsRequired();
        // "#RRGGBB" or "#RRGGBBAA" — 9 chars max. Stored as the literal CSS
        // hex string so the picker round-trips cleanly.
        b.Property(x => x.BackgroundHex).HasMaxLength(9).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        // One style per tag per project. The unique index is case-sensitive,
        // which is fine because Issue.SetLabels dedupes incoming labels
        // case-insensitively (first-casing-wins), so a project never ends up
        // with both "backend" and "Backend" on its issues.
        b.HasIndex(x => new { x.ProjectId, x.Tag }).IsUnique();
    }
}
