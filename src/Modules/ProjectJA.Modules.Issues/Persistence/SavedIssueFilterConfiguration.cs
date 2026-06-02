// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Persistence;

public sealed class SavedIssueFilterConfiguration : IEntityTypeConfiguration<SavedIssueFilter>
{
    public void Configure(EntityTypeBuilder<SavedIssueFilter> b)
    {
        b.ToTable("saved_issue_filters");
        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).IsRequired();
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Name).HasMaxLength(80).IsRequired();
        // Opaque UI-shape JSON. jsonb so future migrations can index into it
        // (e.g. "list filters that scope to assignee X") without a schema bump.
        b.Property(x => x.FilterJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();

        // One filter per name within a (user, project). Case-sensitive on
        // Name — matches how IssuePriority and other case-sensitive UI labels
        // already work; if a user wants "Open" and "open" as distinct names
        // they can have them.
        b.HasIndex(x => new { x.UserId, x.ProjectId, x.Name }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.ProjectId });
    }
}
