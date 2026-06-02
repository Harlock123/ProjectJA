// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

public sealed record SavedFilterSummary(
    Guid Id, string Name, string FilterJson, DateTimeOffset UpdatedAt);

public interface ISavedIssueFilters
{
    Task<IReadOnlyList<SavedFilterSummary>> ListAsync(Guid userId, Guid projectId, CancellationToken ct);

    /// <summary>Upsert by (userId, projectId, name) — saving with an existing
    /// name overwrites the stored JSON. Returns the row id (new or existing).</summary>
    Task<Guid> SaveAsync(Guid userId, Guid projectId, string name, string filterJson, CancellationToken ct);

    /// <summary>Delete by id; only succeeds when the row's userId matches —
    /// users can only ever delete their own saved filters.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid filterId, CancellationToken ct);
}
