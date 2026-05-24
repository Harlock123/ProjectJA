// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Notifications.Domain;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.SharedKernel.Notifications;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Notifications.Application;

internal sealed class NotificationService(DbContext db, IClock clock) : INotificationService
{
    public async Task<IReadOnlyList<NotificationView>> ListForUserAsync(Guid userId, int limit, CancellationToken ct)
    {
        var clamped = Math.Clamp(limit, 1, 100);
        return await db.Set<Notification>()
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(clamped)
            .Select(n => new NotificationView(
                n.Id, n.ActorId, n.Kind, n.Title, n.Link, n.ResourceId, n.CreatedAt, n.ReadAt))
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct)
    {
        return await db.Set<Notification>()
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && n.ReadAt == null, ct);
    }

    public async Task MarkReadAsync(Guid notificationId, Guid actingUserId, CancellationToken ct)
    {
        var row = await db.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == actingUserId, ct);
        if (row is null) return;
        row.MarkRead(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.Set<Notification>()
            .Where(n => n.RecipientUserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task OnIssueAssignedAsync(
        Guid issueId, Guid? previousAssigneeId, Guid? newAssigneeId, Guid actorId,
        CancellationToken ct)
    {
        // Nothing to do when assignment is cleared, unchanged, or self-assigned.
        if (newAssigneeId is null) return;
        if (newAssigneeId == previousAssigneeId) return;
        if (newAssigneeId == actorId) return;

        var info = await LoadIssueRefAsync(issueId, ct);
        if (info is null) return;

        var title = $"You were assigned {info.Value.Key}-{info.Value.Number}: {Truncate(info.Value.Title, 140)}";
        db.Set<Notification>().Add(Notification.Create(
            recipientUserId: newAssigneeId.Value,
            actorId: actorId,
            kind: NotificationKinds.IssueAssigned,
            title: title,
            link: $"/issues/{issueId}",
            resourceId: issueId,
            now: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task OnCommentAddedAsync(Guid issueId, Guid commentAuthorId, CancellationToken ct)
    {
        var info = await LoadIssueRefAsync(issueId, ct);
        if (info is null) return;

        // Notify the assignee + reporter, minus the comment author (no self-
        // notifications). If both are the same user, dedupe.
        var recipients = new HashSet<Guid>();
        if (info.Value.AssigneeId is { } aid && aid != commentAuthorId) recipients.Add(aid);
        if (info.Value.ReporterId != Guid.Empty && info.Value.ReporterId != commentAuthorId)
            recipients.Add(info.Value.ReporterId);
        if (recipients.Count == 0) return;

        var title = $"New comment on {info.Value.Key}-{info.Value.Number}: {Truncate(info.Value.Title, 140)}";
        var now = clock.UtcNow;
        foreach (var uid in recipients)
        {
            db.Set<Notification>().Add(Notification.Create(
                recipientUserId: uid,
                actorId: commentAuthorId,
                kind: NotificationKinds.IssueCommented,
                title: title,
                link: $"/issues/{issueId}",
                resourceId: issueId,
                now: now));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task OnIssueTransitionedAsync(
        Guid issueId, bool isDoneCategory, Guid actorId, CancellationToken ct)
    {
        if (!isDoneCategory) return;

        var info = await LoadIssueRefAsync(issueId, ct);
        if (info is null) return;

        var recipients = new HashSet<Guid>();
        if (info.Value.AssigneeId is { } aid && aid != actorId) recipients.Add(aid);
        if (info.Value.ReporterId != Guid.Empty && info.Value.ReporterId != actorId)
            recipients.Add(info.Value.ReporterId);
        if (recipients.Count == 0) return;

        var title = $"{info.Value.Key}-{info.Value.Number} marked Done: {Truncate(info.Value.Title, 140)}";
        var now = clock.UtcNow;
        foreach (var uid in recipients)
        {
            db.Set<Notification>().Add(Notification.Create(
                recipientUserId: uid, actorId: actorId,
                kind: NotificationKinds.IssueDone,
                title: title, link: $"/issues/{issueId}",
                resourceId: issueId, now: now));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task OnBlockerAddedAsync(
        Guid blockedIssueId, Guid blockerIssueId, Guid actorId, CancellationToken ct)
    {
        var blocked = await LoadIssueRefAsync(blockedIssueId, ct);
        if (blocked is null) return;
        // Only notify if the BLOCKED issue has an assignee distinct from the
        // actor — they're the one whose work just got gated.
        if (blocked.Value.AssigneeId is not { } assigneeId || assigneeId == actorId)
            return;

        var blocker = await LoadIssueRefAsync(blockerIssueId, ct);
        var blockerKey = blocker is { } b ? $"{b.Key}-{b.Number}" : "another issue";
        var title = $"{blocked.Value.Key}-{blocked.Value.Number} is now blocked by {blockerKey}";

        db.Set<Notification>().Add(Notification.Create(
            recipientUserId: assigneeId, actorId: actorId,
            kind: NotificationKinds.IssueBlockerAdded,
            title: title, link: $"/issues/{blockedIssueId}",
            resourceId: blockedIssueId, now: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public async Task OnIssueSprintChangedAsync(
        Guid issueId, Guid? previousSprintId, Guid? newSprintId, Guid actorId,
        CancellationToken ct)
    {
        if (previousSprintId == newSprintId) return;
        var info = await LoadIssueRefAsync(issueId, ct);
        if (info is null) return;
        if (info.Value.AssigneeId is not { } assigneeId || assigneeId == actorId) return;

        var newSprintName = newSprintId is { } nsid ? await LoadSprintNameAsync(nsid, ct) : null;
        var title = (previousSprintId, newSprintName) switch
        {
            (null, not null) => $"{info.Value.Key}-{info.Value.Number} moved into sprint '{newSprintName}'",
            (not null, not null) => $"{info.Value.Key}-{info.Value.Number} moved to sprint '{newSprintName}'",
            (not null, null) => $"{info.Value.Key}-{info.Value.Number} returned to backlog",
            _ => $"{info.Value.Key}-{info.Value.Number} sprint changed",
        };

        db.Set<Notification>().Add(Notification.Create(
            recipientUserId: assigneeId, actorId: actorId,
            kind: NotificationKinds.IssueSprintChanged,
            title: title, link: $"/issues/{issueId}",
            resourceId: issueId, now: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public Task OnPasswordChangedAsync(Guid userId, CancellationToken ct) =>
        RecordSecuritySelfAsync(userId, NotificationKinds.SecurityPasswordChanged,
            "Your password was changed.", ct);

    public Task OnTwoFactorEnabledAsync(Guid userId, CancellationToken ct) =>
        RecordSecuritySelfAsync(userId, NotificationKinds.SecurityTwoFactorEnabled,
            "Two-factor authentication was enabled on your account.", ct);

    public async Task OnTwoFactorDisabledAsync(Guid targetUserId, Guid? adminActorId, CancellationToken ct)
    {
        var byAdmin = adminActorId is not null && adminActorId.Value != targetUserId;
        var kind = byAdmin
            ? NotificationKinds.SecurityTwoFactorAdminDisabled
            : NotificationKinds.SecurityTwoFactorDisabled;
        var title = byAdmin
            ? "An organization admin disabled two-factor authentication on your account."
            : "Two-factor authentication was disabled on your account.";

        db.Set<Notification>().Add(Notification.Create(
            recipientUserId: targetUserId,
            actorId: byAdmin ? adminActorId : null,
            kind: kind,
            title: title, link: "/settings",
            resourceId: null, now: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    public Task OnRecoveryCodesRegeneratedAsync(Guid userId, CancellationToken ct) =>
        RecordSecuritySelfAsync(userId, NotificationKinds.SecurityRecoveryCodesRegenerated,
            "New two-factor recovery codes were generated for your account.", ct);

    // Shared insert path for the self-initiated security events. actorId is
    // null (the user did it themselves; capturing "you" as actor adds nothing).
    private async Task RecordSecuritySelfAsync(Guid userId, string kind, string title, CancellationToken ct)
    {
        db.Set<Notification>().Add(Notification.Create(
            recipientUserId: userId, actorId: null,
            kind: kind, title: title, link: "/settings",
            resourceId: null, now: clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private readonly record struct IssueRef(
        Guid Id, int Number, string Title, string Key, Guid? AssigneeId, Guid ReporterId);

    private async Task<IssueRef?> LoadIssueRefAsync(Guid issueId, CancellationToken ct)
    {
        // One round-trip to pull just the fields we need (the issue row + the
        // project key for the human title).
        return await db.Set<Issue>().AsNoTracking()
            .Where(i => i.Id == issueId)
            .Join(db.Set<Project>().AsNoTracking(), i => i.ProjectId, p => p.Id,
                (i, p) => new IssueRef(i.Id, i.Number, i.Title, p.Key, i.AssigneeId, i.ReporterId))
            .Cast<IssueRef?>()
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string?> LoadSprintNameAsync(Guid sprintId, CancellationToken ct)
    {
        return await db.Set<Sprint>().AsNoTracking()
            .Where(s => s.Id == sprintId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
