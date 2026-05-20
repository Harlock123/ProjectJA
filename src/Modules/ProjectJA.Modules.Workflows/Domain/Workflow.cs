// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Workflows.Domain;

/// <summary>A project's status pipeline. One workflow per project (we don't
/// share workflows across projects in this slice); admins can add / rename /
/// reorder / delete states. Transitions are any-to-any in this slice; the
/// domain does not constrain which moves are allowed. Cross-aggregate FK to
/// project (no DB-level FK — matches the modular monolith pattern used for
/// <c>Issue.ProjectId</c> / <c>Issue.SprintId</c>).</summary>
public sealed class Workflow
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    /// <summary>True for the workflow auto-seeded when the project is created.
    /// Lets the UI distinguish "yes, this is the system-provided baseline" from
    /// a user-renamed one. The flag is informational; the domain enforces no
    /// special protection on default workflows.</summary>
    public bool IsDefault { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private readonly List<WorkflowState> _states = new();
    public IReadOnlyList<WorkflowState> States => _states;

    private Workflow() { }

    private Workflow(Guid id, Guid projectId, string name, bool isDefault, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name is required.", nameof(name));
        Id = id;
        ProjectId = projectId;
        Name = name;
        IsDefault = isDefault;
        CreatedAt = createdAt;
    }

    /// <summary>Create a workflow with the system-default Todo/Doing/Done set
    /// of states. Used by <c>IWorkflowSeeder</c> on new project creation and by
    /// the schema-migration's data-move for every pre-existing project.</summary>
    public static Workflow CreateDefault(Guid projectId, DateTimeOffset now)
    {
        var wf = new Workflow(Guid.NewGuid(), projectId, "Default", isDefault: true, now);
        wf._states.Add(new WorkflowState(Guid.NewGuid(), wf.Id, "Todo", 0, WorkflowStateCategory.Open));
        wf._states.Add(new WorkflowState(Guid.NewGuid(), wf.Id, "Doing", 1, WorkflowStateCategory.InProgress));
        wf._states.Add(new WorkflowState(Guid.NewGuid(), wf.Id, "Done", 2, WorkflowStateCategory.Done));
        return wf;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name is required.", nameof(name));
        Name = name;
        // A user-renamed workflow is no longer the default baseline.
        IsDefault = false;
    }

    /// <summary>Append a new state at the end of the order.</summary>
    public WorkflowState AddState(string name, WorkflowStateCategory category)
    {
        var nextOrder = _states.Count == 0 ? 0 : _states.Max(s => s.Order) + 1;
        var state = new WorkflowState(Guid.NewGuid(), Id, name, nextOrder, category);
        _states.Add(state);
        return state;
    }

    public void RenameState(Guid stateId, string name)
    {
        var s = FindOrThrow(stateId);
        s.Rename(name);
    }

    public void RecategorizeState(Guid stateId, WorkflowStateCategory category)
    {
        var s = FindOrThrow(stateId);
        s.SetCategory(category);
    }

    /// <summary>Remove a state from the workflow. The caller (application layer)
    /// must check no issues reference it — the domain doesn't have that view.
    /// Refuses to leave the workflow with fewer than two states (need at least
    /// one Open-ish and one Done-ish to be useful), or to remove the last
    /// state in either the Open or Done category.</summary>
    public void RemoveState(Guid stateId)
    {
        var s = FindOrThrow(stateId);
        if (_states.Count <= 2)
            throw new InvalidOperationException("A workflow needs at least two states.");
        var remainingInCat = _states.Count(x => x.Category == s.Category && x.Id != stateId);
        if (s.Category is WorkflowStateCategory.Open or WorkflowStateCategory.Done && remainingInCat == 0)
            throw new InvalidOperationException(
                $"Can't remove the last {s.Category} state — every workflow needs at least one Open and one Done state.");
        _states.Remove(s);
        // Compact the order so downstream code doesn't see gaps.
        var ordered = _states.OrderBy(x => x.Order).ToList();
        for (var i = 0; i < ordered.Count; i++) ordered[i].SetOrder(i);
    }

    /// <summary>Apply a new ordering. <paramref name="stateIdsInOrder"/> must
    /// be exactly the current set of state ids (same count, same ids). The
    /// resulting <c>Order</c> values are 0..N-1 in the given sequence.</summary>
    public void ReorderStates(IReadOnlyList<Guid> stateIdsInOrder)
    {
        if (stateIdsInOrder.Count != _states.Count)
            throw new InvalidOperationException("Reorder list must contain every state exactly once.");
        var byId = _states.ToDictionary(s => s.Id);
        if (stateIdsInOrder.Any(id => !byId.ContainsKey(id)))
            throw new InvalidOperationException("Reorder list contains an unknown state id.");
        if (stateIdsInOrder.Distinct().Count() != stateIdsInOrder.Count)
            throw new InvalidOperationException("Reorder list has duplicate state ids.");
        for (var i = 0; i < stateIdsInOrder.Count; i++)
            byId[stateIdsInOrder[i]].SetOrder(i);
    }

    private WorkflowState FindOrThrow(Guid stateId) =>
        _states.FirstOrDefault(s => s.Id == stateId)
            ?? throw new InvalidOperationException("State not found in this workflow.");
}
