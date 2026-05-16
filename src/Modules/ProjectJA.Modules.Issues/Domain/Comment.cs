// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

public sealed class Comment
{
    public Guid Id { get; private set; }
    public Guid IssueId { get; private set; }
    public Guid AuthorId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Comment() { }

    public Comment(Guid id, Guid issueId, Guid authorId, string body, DateTimeOffset createdAt)
    {
        Id = id;
        IssueId = issueId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    internal static Comment Create(Guid issueId, Guid authorId, string body, DateTimeOffset now)
        => new(Guid.NewGuid(), issueId, authorId, body, now);
}
