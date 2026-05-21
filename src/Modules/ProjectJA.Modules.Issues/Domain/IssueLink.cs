// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

/// <summary>A directed "blocks" relationship between two issues:
/// <see cref="BlockerIssueId"/> must finish before <see cref="BlockedIssueId"/>
/// can be considered done. The reverse direction ("blocked by") is the same
/// row read from the other end. Self-links are rejected at construction; cycle
/// prevention lives in the application layer because it needs to walk the
/// existing graph.</summary>
public sealed class IssueLink
{
    public Guid Id { get; private set; }
    public Guid BlockerIssueId { get; private set; }
    public Guid BlockedIssueId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private IssueLink() { }

    public IssueLink(Guid id, Guid blockerIssueId, Guid blockedIssueId, Guid createdById, DateTimeOffset now)
    {
        if (blockerIssueId == blockedIssueId)
            throw new InvalidOperationException("An issue cannot block itself.");
        Id = id;
        BlockerIssueId = blockerIssueId;
        BlockedIssueId = blockedIssueId;
        CreatedById = createdById;
        CreatedAt = now;
    }

    public static IssueLink Create(Guid blockerIssueId, Guid blockedIssueId, Guid createdById, DateTimeOffset now)
        => new(Guid.NewGuid(), blockerIssueId, blockedIssueId, createdById, now);
}
