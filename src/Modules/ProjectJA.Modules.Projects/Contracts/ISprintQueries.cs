// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Contracts;

public sealed record SprintSummary(
    Guid Id,
    Guid ProjectId,
    string Name,
    string? Goal,
    SprintStatus Status,
    DateTimeOffset? PlannedStart,
    DateTimeOffset? PlannedEnd,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public interface ISprintQueries
{
    Task<IReadOnlyList<SprintSummary>> ListForProjectAsync(Guid projectId, CancellationToken ct);

    Task<SprintSummary?> GetByIdAsync(Guid sprintId, CancellationToken ct);

    /// <summary>Whether the project already has an Active sprint — enforces the
    /// one-Active-per-project invariant at the endpoint layer.</summary>
    Task<bool> HasActiveSprintAsync(Guid projectId, CancellationToken ct);
}
