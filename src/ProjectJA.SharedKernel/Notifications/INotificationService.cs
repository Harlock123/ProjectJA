// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Notifications;

public sealed record NotificationView(
    Guid Id,
    Guid? ActorId,
    string Kind,
    string Title,
    string Link,
    Guid? ResourceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>Static well-known event kinds. Kept as strings (not an enum) so
/// adding new event sources doesn't require a schema/migration change.</summary>
public static class NotificationKinds
{
    public const string IssueAssigned  = "issue.assigned";
    public const string IssueCommented = "issue.commented";
}

/// <summary>Cross-module facade for raising and reading in-app notifications.
/// Lives in SharedKernel (alongside <c>IAuditLog</c>, <c>IEmailSender</c>) so
/// any module can call it without creating a circular dependency through the
/// Notifications implementation module.</summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationView>> ListForUserAsync(Guid userId, int limit, CancellationToken ct);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct);
    Task MarkReadAsync(Guid notificationId, Guid actingUserId, CancellationToken ct);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct);

    /// <summary>Fire when an issue's assignee changes. Notifies the new
    /// assignee unless they're the actor making the assignment, or there is no
    /// new assignee (assignment was cleared).</summary>
    Task OnIssueAssignedAsync(
        Guid issueId, Guid? previousAssigneeId, Guid? newAssigneeId, Guid actorId,
        CancellationToken ct);

    /// <summary>Fire when a comment is added. Notifies the issue's assignee
    /// and reporter, minus the comment author (no self-notifications).</summary>
    Task OnCommentAddedAsync(Guid issueId, Guid commentAuthorId, CancellationToken ct);
}
