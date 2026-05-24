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

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
