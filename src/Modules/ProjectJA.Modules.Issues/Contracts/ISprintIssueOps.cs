// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>Cross-module hook the Projects/Sprints endpoints call into. Sprint
/// completion needs to clear <c>Issue.SprintId</c> on still-open issues (push
/// them back to the backlog), but the Projects module doesn't reference Issues
/// — so the Issues module exposes this small contract instead, preserving the
/// one-way Issues→Projects module dependency.</summary>
public interface ISprintIssueOps
{
    /// <summary>Set <c>SprintId = null</c> on every issue currently in
    /// <paramref name="sprintId"/> whose <c>Status</c> is not Done. Returns the
    /// number of issues that were moved back to the backlog.</summary>
    Task<int> ClearSprintForIncompleteAsync(Guid sprintId, DateTimeOffset now, CancellationToken ct);

    /// <summary>True if any issue currently references this sprint — gates the
    /// "delete only when empty" rule from the Projects-side DELETE endpoint.</summary>
    Task<bool> HasAnyIssuesInSprintAsync(Guid sprintId, CancellationToken ct);
}
