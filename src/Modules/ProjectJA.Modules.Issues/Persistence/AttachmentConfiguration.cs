// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Persistence;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.ToTable("issue_attachments");
        b.HasKey(x => x.Id);
        b.Property(x => x.IssueId).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(400).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(200).IsRequired();
        b.Property(x => x.SizeBytes).IsRequired();
        b.Property(x => x.StorageKey).HasMaxLength(500).IsRequired();
        b.Property(x => x.UploadedBy).IsRequired();
        b.Property(x => x.UploadedAt).IsRequired();
        b.HasIndex(x => x.IssueId);
    }
}
