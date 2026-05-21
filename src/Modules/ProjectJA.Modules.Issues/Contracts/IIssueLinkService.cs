// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>One side of a blocker relationship, projected for display: either
/// an issue that blocks the one being viewed, or one that's blocked by it
/// (the consumer picks the side).</summary>
public sealed record IssueLinkSummary(Guid IssueId, int Number, string Title);

public sealed record IssueLinksView(
    IReadOnlyList<IssueLinkSummary> BlockedBy,
    IReadOnlyList<IssueLinkSummary> Blocks);

/// <summary>Result of an add-blocker attempt — discriminates the various
/// validation failures so callers can surface the right message without
/// catching exceptions.</summary>
public enum AddBlockerOutcome
{
    Added,
    NotFound,
    SelfLink,
    CrossProject,
    Duplicate,
    Cycle,
}

public interface IIssueLinkService
{
    Task<IssueLinksView> GetLinksAsync(Guid issueId, CancellationToken ct);
    Task<AddBlockerOutcome> AddBlockerAsync(Guid blockedIssueId, Guid blockerIssueId, Guid actingUserId, CancellationToken ct);
    Task<bool> RemoveBlockerAsync(Guid blockedIssueId, Guid blockerIssueId, CancellationToken ct);
}
