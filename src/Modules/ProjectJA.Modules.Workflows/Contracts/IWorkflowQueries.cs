// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Workflows.Contracts;

public sealed record WorkflowStateView(
    Guid Id,
    string Name,
    int Order,
    WorkflowStateCategory Category);

public sealed record WorkflowTransitionView(Guid FromStateId, Guid ToStateId);

public sealed record WorkflowView(
    Guid Id,
    Guid ProjectId,
    string Name,
    bool IsDefault,
    DateTimeOffset CreatedAt,
    IReadOnlyList<WorkflowStateView> States,
    IReadOnlyList<WorkflowTransitionView> Transitions)
{
    /// <summary>Allowed-move check matching <c>Workflow.IsTransitionAllowed</c>;
    /// callers that already loaded the view can ask without another round trip.</summary>
    public bool IsTransitionAllowed(Guid fromStateId, Guid toStateId) =>
        fromStateId == toStateId
        || Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId);
}

public interface IWorkflowQueries
{
    /// <summary>Returns the project's single workflow, with its states ordered.
    /// Null if the project doesn't have one (shouldn't normally happen — the
    /// seeder runs on project creation + the data migration seeds existing).</summary>
    Task<WorkflowView?> GetForProjectAsync(Guid projectId, CancellationToken ct);

    Task<WorkflowStateView?> GetStateAsync(Guid stateId, CancellationToken ct);

    /// <summary>Whether any issue currently references the given state — gates
    /// the "delete only when empty" rule on the DELETE endpoint.</summary>
    Task<bool> HasIssueInStateAsync(Guid stateId, CancellationToken ct);
}
