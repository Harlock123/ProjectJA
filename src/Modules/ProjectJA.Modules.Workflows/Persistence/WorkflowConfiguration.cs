// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Workflows.Persistence;

public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
{
    public void Configure(EntityTypeBuilder<Workflow> b)
    {
        b.ToTable("workflows");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.IsDefault).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.HasIndex(x => x.ProjectId);

        // Owned states — same pattern as ProjectMember on Project.
        b.OwnsMany(x => x.States, s =>
        {
            s.ToTable("workflow_states");
            s.WithOwner().HasForeignKey(x => x.WorkflowId);
            s.HasKey(x => x.Id);
            s.Property(x => x.Name).HasMaxLength(200).IsRequired();
            s.Property(x => x.Order).IsRequired();
            s.Property(x => x.Category).HasConversion<int>().IsRequired();
            s.HasIndex(x => x.WorkflowId);
            s.HasIndex(x => new { x.WorkflowId, x.Order });
        });

        // Owned allowed-transition pairs. UNIQUE (WorkflowId, From, To) so the
        // matrix can't have duplicates; same-state pairs never inserted by the
        // domain so the constraint is "directional unique" only.
        b.OwnsMany(x => x.Transitions, t =>
        {
            t.ToTable("workflow_transitions");
            t.WithOwner().HasForeignKey(x => x.WorkflowId);
            t.HasKey(x => x.Id);
            t.Property(x => x.FromStateId).IsRequired();
            t.Property(x => x.ToStateId).IsRequired();
            t.HasIndex(x => new { x.WorkflowId, x.FromStateId, x.ToStateId }).IsUnique();
        });
    }
}
