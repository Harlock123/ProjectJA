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

    /// <summary>The background color (CSS hex) saved for each styled tag in
    /// the project. Tags without a stored style are absent from the map; the
    /// renderer falls back to the default outlined look. Keys preserve the
    /// stored casing.</summary>
    Task<IReadOnlyDictionary<string, string>> GetStylesAsync(Guid projectId, CancellationToken ct);

    /// <summary>Upsert the background color for one tag. Pass a value like
    /// "#3366cc" (with or without an alpha byte). Empty / whitespace is
    /// treated as "clear style" — same as <see cref="ClearStyleAsync"/>.</summary>
    Task SetStyleAsync(Guid projectId, string tag, string backgroundHex, CancellationToken ct);

    /// <summary>Drop the stored style for a tag, reverting it to the default
    /// outlined chip. No-op if no style was stored.</summary>
    Task ClearStyleAsync(Guid projectId, string tag, CancellationToken ct);

    /// <summary>Copy <paramref name="sourceTag"/>'s background colour onto
    /// every tag in <paramref name="targetTags"/>. Targets without an existing
    /// style row are inserted; targets that already have one are overwritten.
    /// Returns the number of target rows written. Throws if the source has no
    /// style to copy.</summary>
    Task<int> CopyStyleAsync(Guid projectId, string sourceTag, IEnumerable<string> targetTags, CancellationToken ct);

    /// <summary>"Ripple" a colour change through a tag group: read the source
    /// tag's CURRENT colour, find every other tag in the project that shares
    /// that exact colour, and overwrite all of them (including the source)
    /// with <paramref name="newBackgroundHex"/>. Returns the total number of
    /// tag rows updated. Throws if the source has no current style — there's
    /// no "group" to ripple through.</summary>
    Task<int> RippleAsync(Guid projectId, string sourceTag, string newBackgroundHex, CancellationToken ct);
}
