// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Persistence;

public sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> b)
    {
        b.ToTable("sprints");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Goal).HasMaxLength(2000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.PlannedStart);
        b.Property(x => x.PlannedEnd);
        b.Property(x => x.StartedAt);
        b.Property(x => x.CompletedAt);
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => x.ProjectId);
        b.HasIndex(x => new { x.ProjectId, x.Status });
        // No DB-level FK to projects — keeps the Issues→Sprint relationship at
        // application level too, matching how Issue.ProjectId is modelled (just
        // an index, no HasOne). Sprint deletion is gated by the API to
        // "Planned + no assigned issues", so no dangling rows in practice.
    }
}
