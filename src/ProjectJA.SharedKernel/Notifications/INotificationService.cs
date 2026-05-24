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
    public const string IssueAssigned       = "issue.assigned";
    public const string IssueCommented      = "issue.commented";
    public const string CommentMentioned    = "comment.mentioned";
    public const string IssueDone           = "issue.done";
    public const string IssueBlockerAdded   = "issue.blocker_added";
    public const string IssueSprintChanged  = "issue.sprint_changed";

    public const string SecurityPasswordChanged           = "security.password_changed";
    public const string SecurityTwoFactorEnabled          = "security.two_factor_enabled";
    public const string SecurityTwoFactorDisabled         = "security.two_factor_disabled";
    public const string SecurityTwoFactorAdminDisabled    = "security.two_factor_admin_disabled";
    public const string SecurityRecoveryCodesRegenerated  = "security.recovery_codes_regenerated";
}

/// <summary>Cross-module facade for raising and reading in-app notifications.
/// Lives in SharedKernel (alongside <c>IAuditLog</c>, <c>IEmailSender</c>) so
/// any module can call it without creating a circular dependency through the
/// Notifications implementation module.</summary>
public interface INotificationService
{
    /// <summary>List the user's most recent notifications, newest first.
    /// Pass <paramref name="unreadOnly"/> = true to restrict to unread (the
    /// bell popover uses this so the visible list stays in sync with the
    /// badge count).</summary>
    Task<IReadOnlyList<NotificationView>> ListForUserAsync(Guid userId, int limit, bool unreadOnly, CancellationToken ct);

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
    /// and reporter, minus the comment author (no self-notifications).
    /// Additionally parses <paramref name="body"/> for <c>@email-prefix</c>
    /// mentions and raises a <c>comment.mentioned</c> notification for each
    /// resolved user. When a user would otherwise receive both the generic
    /// "issue.commented" and a "comment.mentioned", only the mention fires —
    /// it's the higher-signal notification.</summary>
    Task OnCommentAddedAsync(Guid issueId, Guid commentAuthorId, string body, CancellationToken ct);

    /// <summary>Fire AFTER an issue transitions to a workflow state. Only
    /// produces a notification when <paramref name="isDoneCategory"/> is true.
    /// Recipients: reporter + assignee minus the actor (the actor already
    /// knows they did it).</summary>
    Task OnIssueTransitionedAsync(
        Guid issueId, bool isDoneCategory, Guid actorId, CancellationToken ct);

    /// <summary>Fire when a blocker is added to an issue. Notifies the
    /// assignee of the BLOCKED issue (minus the actor).</summary>
    Task OnBlockerAddedAsync(
        Guid blockedIssueId, Guid blockerIssueId, Guid actorId, CancellationToken ct);

    /// <summary>Fire when an issue's sprint assignment changes. Notifies the
    /// assignee (minus the actor). Title varies for "into sprint" / "between
    /// sprints" / "back to backlog".</summary>
    Task OnIssueSprintChangedAsync(
        Guid issueId, Guid? previousSprintId, Guid? newSprintId, Guid actorId,
        CancellationToken ct);

    // ---- Security self-notifications (paper trail for the account owner) ----

    Task OnPasswordChangedAsync(Guid userId, CancellationToken ct);
    Task OnTwoFactorEnabledAsync(Guid userId, CancellationToken ct);

    /// <summary>Pass a non-null <paramref name="adminActorId"/> distinct from
    /// <paramref name="targetUserId"/> to record an admin-initiated disable;
    /// otherwise this is a self-disable from Settings.</summary>
    Task OnTwoFactorDisabledAsync(Guid targetUserId, Guid? adminActorId, CancellationToken ct);

    Task OnRecoveryCodesRegeneratedAsync(Guid userId, CancellationToken ct);
}
