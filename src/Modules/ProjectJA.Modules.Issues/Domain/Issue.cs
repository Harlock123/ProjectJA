// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

public sealed class Issue
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public int Number { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public IssueStatus Status { get; private set; }
    public Guid? AssigneeId { get; private set; }
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<Comment> _comments = new();
    public IReadOnlyList<Comment> Comments => _comments;

    private Issue() { }

    public Issue(Guid id, Guid projectId, int number, string title, string? description, Guid createdById, DateTimeOffset now)
    {
        Id = id;
        ProjectId = projectId;
        Number = number;
        Title = title;
        Description = description;
        Status = IssueStatus.Todo;
        CreatedById = createdById;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static Issue Create(Guid projectId, int number, string title, string? description, Guid createdById, DateTimeOffset now)
        => new(Guid.NewGuid(), projectId, number, title, description, createdById, now);

    public void Edit(string title, string? description, DateTimeOffset now)
    {
        Title = title;
        Description = description;
        UpdatedAt = now;
    }

    public void Transition(IssueStatus newStatus, DateTimeOffset now)
    {
        Status = newStatus;
        UpdatedAt = now;
    }

    public void Assign(Guid? assigneeId, DateTimeOffset now)
    {
        AssigneeId = assigneeId;
        UpdatedAt = now;
    }

    public Comment AddComment(Guid authorId, string body, DateTimeOffset now)
    {
        var c = Comment.Create(Id, authorId, body, now);
        _comments.Add(c);
        UpdatedAt = now;
        return c;
    }
}
