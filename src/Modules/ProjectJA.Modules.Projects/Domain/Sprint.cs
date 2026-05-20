// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>A time-boxed iteration belonging to a single project. Issues opt
/// into a sprint via <c>Issue.SprintId</c>; null = backlog. Lifecycle is
/// strictly Planned → Active → Completed (see <see cref="SprintStatus"/>);
/// once Completed nothing on the sprint can be edited.</summary>
public sealed class Sprint
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Goal { get; private set; }
    public SprintStatus Status { get; private set; }
    public DateTimeOffset? PlannedStart { get; private set; }
    public DateTimeOffset? PlannedEnd { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Sprint() { }

    public Sprint(Guid id, Guid projectId, string name, string? goal,
        DateTimeOffset? plannedStart, DateTimeOffset? plannedEnd, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Sprint name is required.", nameof(name));
        Id = id;
        ProjectId = projectId;
        Name = name;
        Goal = string.IsNullOrWhiteSpace(goal) ? null : goal;
        Status = SprintStatus.Planned;
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
        CreatedAt = now;
    }

    public static Sprint Create(Guid projectId, string name, string? goal,
        DateTimeOffset? plannedStart, DateTimeOffset? plannedEnd, DateTimeOffset now)
        => new(Guid.NewGuid(), projectId, name, goal, plannedStart, plannedEnd, now);

    public void Edit(string name, string? goal, DateTimeOffset? plannedStart, DateTimeOffset? plannedEnd)
    {
        if (Status == SprintStatus.Completed)
            throw new InvalidOperationException("A completed sprint can't be edited.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Sprint name is required.", nameof(name));
        Name = name;
        Goal = string.IsNullOrWhiteSpace(goal) ? null : goal;
        PlannedStart = plannedStart;
        PlannedEnd = plannedEnd;
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != SprintStatus.Planned)
            throw new InvalidOperationException($"Only a Planned sprint can be started (this one is {Status}).");
        Status = SprintStatus.Active;
        StartedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != SprintStatus.Active)
            throw new InvalidOperationException($"Only an Active sprint can be completed (this one is {Status}).");
        Status = SprintStatus.Completed;
        CompletedAt = now;
    }
}
