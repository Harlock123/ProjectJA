// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Workflows.Domain;

/// <summary>An allowed move from one workflow state to another, owned by the
/// parent <see cref="Workflow"/>. Pairs are directional — an allow rule for
/// (Open → Done) does NOT also allow (Done → Open). Self-pairs are never
/// stored; same-state "transitions" are no-ops handled at the call site.</summary>
public sealed class WorkflowTransition
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public Guid FromStateId { get; private set; }
    public Guid ToStateId { get; private set; }

    private WorkflowTransition() { }

    internal WorkflowTransition(Guid id, Guid workflowId, Guid fromStateId, Guid toStateId)
    {
        if (fromStateId == toStateId)
            throw new ArgumentException("Self-transitions are not stored.", nameof(fromStateId));
        Id = id;
        WorkflowId = workflowId;
        FromStateId = fromStateId;
        ToStateId = toStateId;
    }
}
