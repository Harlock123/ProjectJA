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
    public IssueType Type { get; private set; }
    public int? Points { get; private set; }
    public string? AcceptanceCriteria { get; private set; }
    public Guid? AssigneeId { get; private set; }
    /// <summary>Who reported/recorded the issue. Defaults to the creator; editable.</summary>
    public Guid ReporterId { get; private set; }
    /// <summary>Immutable system record of who actually created the row.</summary>
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
        Type = IssueType.Task;
        CreatedById = createdById;
        ReporterId = createdById;
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

    /// <summary>Update the agile classification: type, story points, and DoD/acceptance criteria.</summary>
    public void Reclassify(IssueType type, int? points, string? acceptanceCriteria, DateTimeOffset now)
    {
        Type = type;
        Points = points;
        AcceptanceCriteria = acceptanceCriteria;
        UpdatedAt = now;
    }

    public void Assign(Guid? assigneeId, DateTimeOffset now)
    {
        AssigneeId = assigneeId;
        UpdatedAt = now;
    }

    public void SetReporter(Guid reporterId, DateTimeOffset now)
    {
        ReporterId = reporterId;
        UpdatedAt = now;
    }

    public Comment AddComment(Guid authorId, string body, DateTimeOffset now)
    {
        var c = Comment.Create(Id, authorId, body, now);
        _comments.Add(c);
        UpdatedAt = now;
        return c;
    }

    /// <summary>Edit a comment's body. Only the original author may do so.</summary>
    public void EditComment(Guid commentId, Guid actingUserId, string body, DateTimeOffset now)
    {
        var c = _comments.FirstOrDefault(x => x.Id == commentId)
            ?? throw new InvalidOperationException("Comment not found.");
        if (c.AuthorId != actingUserId)
            throw new InvalidOperationException("Only the comment's author can edit it.");
        c.UpdateBody(body);
        UpdatedAt = now;
    }

    /// <summary>Delete a comment. Only the original author may do so.</summary>
    public void RemoveComment(Guid commentId, Guid actingUserId, DateTimeOffset now)
    {
        var c = _comments.FirstOrDefault(x => x.Id == commentId)
            ?? throw new InvalidOperationException("Comment not found.");
        if (c.AuthorId != actingUserId)
            throw new InvalidOperationException("Only the comment's author can delete it.");
        _comments.Remove(c);
        UpdatedAt = now;
    }
}
