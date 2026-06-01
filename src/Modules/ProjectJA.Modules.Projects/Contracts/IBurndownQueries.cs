// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Contracts;

/// <summary>One day of the burndown chart — the ideal-line value and the
/// reconstructed actual remaining work at end-of-day. Both axes are tracked
/// so the UI can toggle between count and points without a second round-trip.</summary>
public sealed record BurndownDay(
    DateOnly Date,
    double IdealCount,
    double ActualCount,
    double IdealPoints,
    double ActualPoints);

/// <summary>A sprint's burndown snapshot. <see cref="Days"/> spans inclusive
/// from sprint start to either the planned end or <c>today</c>, whichever is
/// later for an in-flight sprint and just the completed-end for a finished
/// one. Totals reflect the issues *currently* assigned to the sprint, which
/// matches the rest of the Sprints UI — scope changes mid-sprint aren't
/// reconstructed in v1.</summary>
public sealed record BurndownView(
    Guid SprintId,
    string SprintName,
    DateOnly Start,
    DateOnly End,
    DateOnly Today,
    int TotalIssues,
    int TotalPoints,
    IReadOnlyList<BurndownDay> Days);

public interface IBurndownQueries
{
    /// <summary>Builds the burndown for a Started or Completed sprint. Returns
    /// null when the sprint is still Planned (nothing to burn down yet), when
    /// the sprint has no issues, or when it doesn't exist.</summary>
    Task<BurndownView?> GetForSprintAsync(Guid sprintId, CancellationToken ct);
}
