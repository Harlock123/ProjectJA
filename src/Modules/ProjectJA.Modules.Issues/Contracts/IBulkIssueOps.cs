// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>Per-issue failure detail surfaced when a bulk transition was
/// rejected by the workflow's transition matrix. The successful count plus
/// the failure list always sum to the total selected.</summary>
public sealed record BulkTransitionFailure(Guid IssueId, string Reason);

public sealed record BulkTransitionResult(
    int Succeeded,
    IReadOnlyList<BulkTransitionFailure> Failed);

public interface IBulkIssueOps
{
    /// <summary>Reassign every selected issue. Passing null assigns to no one.
    /// Returns the number of issues whose AssigneeId actually changed
    /// (already-assigned issues with the same target don't bump UpdatedAt).</summary>
    Task<int> BulkAssignAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid? assigneeId, Guid actorId, bool notify, CancellationToken ct);

    /// <summary>Move every selected issue into the given sprint (null = backlog).
    /// Rejects moves into a Completed sprint and refuses cross-project moves
    /// (defense-in-depth: the page filter already scopes by project).</summary>
    Task<int> BulkAssignToSprintAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid? sprintId, Guid actorId, bool notify, CancellationToken ct);

    /// <summary>Append <paramref name="tag"/> to every selected issue's label
    /// set. Tag normalisation (trim, 40-char cap, case-insensitive dedupe,
    /// 20-tag cap) happens per issue via <c>Issue.SetLabels</c>.</summary>
    Task<int> BulkAddTagAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        string tag, Guid actorId, CancellationToken ct);

    /// <summary>Strip <paramref name="tag"/> from every selected issue that
    /// currently carries it. Case-insensitive match.</summary>
    Task<int> BulkRemoveTagAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        string tag, Guid actorId, CancellationToken ct);

    /// <summary>Move every selected issue into <paramref name="targetStateId"/>.
    /// Per-issue transitions still go through the workflow's allowed-move
    /// matrix; rejections come back in the result's Failed list rather than
    /// throwing — the user expects the rest of the batch to land.</summary>
    Task<BulkTransitionResult> BulkTransitionAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid targetStateId, Guid actorId, bool notify, CancellationToken ct);
}
