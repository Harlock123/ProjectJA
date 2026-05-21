// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Persistence;

public sealed class IssueLinkConfiguration : IEntityTypeConfiguration<IssueLink>
{
    public void Configure(EntityTypeBuilder<IssueLink> b)
    {
        b.ToTable("issue_links");
        b.HasKey(x => x.Id);
        b.Property(x => x.BlockerIssueId).IsRequired();
        b.Property(x => x.BlockedIssueId).IsRequired();
        b.Property(x => x.CreatedById).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();

        // One row per direction; a→b and b→a are distinct rows (different
        // semantics) but a→b twice is rejected.
        b.HasIndex(x => new { x.BlockerIssueId, x.BlockedIssueId }).IsUnique();
        b.HasIndex(x => x.BlockedIssueId);
    }
}
