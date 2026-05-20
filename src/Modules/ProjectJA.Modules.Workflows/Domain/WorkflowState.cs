// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Workflows.Domain;

/// <summary>One named column in a <see cref="Workflow"/>. Owned by the
/// workflow; its lifecycle is managed through the parent aggregate.</summary>
public sealed class WorkflowState
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public string Name { get; private set; } = default!;
    public int Order { get; private set; }
    public WorkflowStateCategory Category { get; private set; }

    private WorkflowState() { }

    internal WorkflowState(Guid id, Guid workflowId, string name, int order, WorkflowStateCategory category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State name is required.", nameof(name));
        Id = id;
        WorkflowId = workflowId;
        Name = name;
        Order = order;
        Category = category;
    }

    internal void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("State name is required.", nameof(name));
        Name = name;
    }

    internal void SetCategory(WorkflowStateCategory category) => Category = category;
    internal void SetOrder(int order) => Order = order;
}
