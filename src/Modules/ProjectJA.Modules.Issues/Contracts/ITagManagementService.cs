// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>One unique tag (label) value, with the count of issues that carry
/// it in a given project.</summary>
public sealed record TagSummary(string Tag, int IssueCount);

public interface ITagManagementService
{
    /// <summary>Distinct tag values used by issues in <paramref name="projectId"/>,
    /// each paired with the number of issues that carry it. Tags returned in
    /// the casing they're stored in (SetLabels uses first-casing-wins).</summary>
    Task<IReadOnlyList<TagSummary>> ListForProjectAsync(Guid projectId, CancellationToken ct);

    /// <summary>Replace every occurrence of <paramref name="oldTag"/> with
    /// <paramref name="newTag"/> across the project's issues. Comparison is
    /// case-insensitive. If an issue already carries the new tag, the rename
    /// dedupes (SetLabels handles this). Returns the number of issues touched.</summary>
    Task<int> RenameAsync(Guid projectId, string oldTag, string newTag, CancellationToken ct);

    /// <summary>Strip <paramref name="tag"/> from every issue in the project
    /// that carries it. Case-insensitive match. Returns the number of issues
    /// touched.</summary>
    Task<int> RemoveAsync(Guid projectId, string tag, CancellationToken ct);
}
