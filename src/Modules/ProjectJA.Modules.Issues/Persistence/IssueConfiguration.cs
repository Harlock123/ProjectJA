// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Persistence;

public sealed class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public const string SearchVectorShadowProperty = "SearchVector";

    public void Configure(EntityTypeBuilder<Issue> b)
    {
        b.ToTable("issues");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProjectId).IsRequired();
        b.Property(x => x.Number).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.Description).HasMaxLength(8000);
        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.Type).HasConversion<int>().IsRequired();
        b.Property(x => x.Priority).HasConversion<int>().IsRequired();
        // Free-text labels → Postgres text[] (Npgsql primitive collection),
        // backed by the read-only Labels/_labels field. No join table / Label
        // aggregate by design (anti-bloat; upgradeable later).
        b.PrimitiveCollection(x => x.Labels);
        b.Property(x => x.Points);
        b.Property(x => x.AcceptanceCriteria).HasMaxLength(8000);
        b.Property(x => x.ReporterId).IsRequired();
        b.Property(x => x.CreatedById).IsRequired();
        b.Property(x => x.CreatedAt).IsRequired();
        b.Property(x => x.UpdatedAt).IsRequired();
        b.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => new { x.ProjectId, x.Type });

        // Postgres FTS: generated tsvector column (weighted: title = A, description = B)
        // with a GIN index for fast indexed search.
        b.Property<NpgsqlTsVector>(SearchVectorShadowProperty)
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', coalesce(\"Title\", '')), 'A') || " +
                "setweight(to_tsvector('english', coalesce(\"Description\", '')), 'B')",
                stored: true);
        b.HasIndex(SearchVectorShadowProperty).HasMethod("gin");

        b.OwnsMany(x => x.Comments, c =>
        {
            c.ToTable("issue_comments");
            c.WithOwner().HasForeignKey(x => x.IssueId);
            c.HasKey(x => x.Id);
            c.Property(x => x.Body).HasMaxLength(8000).IsRequired();
            c.Property(x => x.AuthorId).IsRequired();
            c.Property(x => x.CreatedAt).IsRequired();
        });
    }
}
