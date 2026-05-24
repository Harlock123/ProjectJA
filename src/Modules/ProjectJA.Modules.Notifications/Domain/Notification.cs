// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Notifications.Domain;

/// <summary>One in-app notification addressed to a single recipient. Immutable
/// except for <see cref="ReadAt"/>. Kinds are short stable strings (e.g.
/// "issue.assigned", "issue.commented") so adding new event sources doesn't
/// require a schema change — consumers map kind → icon + colour in the UI.</summary>
public sealed class Notification
{
    public Guid Id { get; private set; }
    /// <summary>The user this notification was raised FOR (the inbox owner).</summary>
    public Guid RecipientUserId { get; private set; }
    /// <summary>The user who triggered the notification (commenter, assigner).
    /// Null when the system itself raised it.</summary>
    public Guid? ActorId { get; private set; }
    public string Kind { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    /// <summary>Relative href to navigate to when the notification is clicked.</summary>
    public string Link { get; private set; } = default!;
    /// <summary>The resource the notification is about (issue id, sprint id, …).
    /// Free-form; used for grouping / dedup if we add that later.</summary>
    public Guid? ResourceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }

    private Notification() { }

    public Notification(
        Guid id, Guid recipientUserId, Guid? actorId,
        string kind, string title, string link, Guid? resourceId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Kind required.", nameof(kind));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required.", nameof(title));
        if (string.IsNullOrWhiteSpace(link)) throw new ArgumentException("Link required.", nameof(link));
        Id = id;
        RecipientUserId = recipientUserId;
        ActorId = actorId;
        Kind = kind;
        Title = title;
        Link = link;
        ResourceId = resourceId;
        CreatedAt = now;
    }

    public static Notification Create(
        Guid recipientUserId, Guid? actorId,
        string kind, string title, string link, Guid? resourceId,
        DateTimeOffset now)
        => new(Guid.NewGuid(), recipientUserId, actorId, kind, title, link, resourceId, now);

    public void MarkRead(DateTimeOffset now)
    {
        if (ReadAt is null) ReadAt = now;
    }
}
