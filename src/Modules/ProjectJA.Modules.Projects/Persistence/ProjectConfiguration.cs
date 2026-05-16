// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Persistence;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrganizationId).IsRequired();
        b.Property(x => x.Key).HasMaxLength(16).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.NextIssueNumber).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
    }
}
