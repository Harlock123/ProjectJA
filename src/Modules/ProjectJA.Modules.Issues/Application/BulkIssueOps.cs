// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Notifications;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class BulkIssueOps(
    DbContext db,
    IClock clock,
    IAuditLog audit,
    INotificationService notifications,
    ISprintQueries sprints,
    IWorkflowQueries workflows) : IBulkIssueOps
{
    public async Task<int> BulkAssignAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid? assigneeId, Guid actorId, bool notify, CancellationToken ct)
    {
        var hits = await LoadInProjectAsync(projectId, issueIds, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        var changed = new List<(Guid IssueId, Guid? Previous)>();
        foreach (var i in hits)
        {
            if (i.AssigneeId == assigneeId) continue;
            var previous = i.AssigneeId;
            i.Assign(assigneeId, now);
            changed.Add((i.Id, previous));
        }
        if (changed.Count == 0) return 0;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "project.issues.bulk_assigned",
            ResourceType: "Project",
            ResourceId: projectId.ToString(),
            Summary: $"Bulk assigned {changed.Count} issue(s) to {(assigneeId is null ? "(unassigned)" : assigneeId.ToString())}",
            Detail: new { issueIds = changed.Select(c => c.IssueId), assigneeId, notify },
            ActorId: actorId), ct);

        if (notify)
        {
            foreach (var c in changed)
            {
                try { await notifications.OnIssueAssignedAsync(c.IssueId, c.Previous, assigneeId, actorId, ct); }
                catch { /* best-effort */ }
            }
        }
        return changed.Count;
    }

    public async Task<int> BulkAssignToSprintAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid? sprintId, Guid actorId, bool notify, CancellationToken ct)
    {
        // Reject moves into a Completed sprint — the single-issue path does
        // the same. ISprintQueries returns null when the sprint doesn't exist
        // or belongs to another project.
        if (sprintId is { } sid)
        {
            var sprint = (await sprints.ListForProjectAsync(projectId, ct))
                .FirstOrDefault(s => s.Id == sid);
            if (sprint is null)
                throw new InvalidOperationException("Target sprint isn't in this project.");
            if (sprint.Status == SprintStatus.Completed)
                throw new InvalidOperationException("Can't move issues into a completed sprint.");
        }

        var hits = await LoadInProjectAsync(projectId, issueIds, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        var changed = new List<(Guid IssueId, Guid? Previous)>();
        foreach (var i in hits)
        {
            if (i.SprintId == sprintId) continue;
            var previous = i.SprintId;
            i.AssignToSprint(sprintId, now);
            changed.Add((i.Id, previous));
        }
        if (changed.Count == 0) return 0;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "project.issues.bulk_sprint_changed",
            ResourceType: "Project",
            ResourceId: projectId.ToString(),
            Summary: $"Bulk moved {changed.Count} issue(s) to {(sprintId is null ? "backlog" : sprintId.ToString())}",
            Detail: new { issueIds = changed.Select(c => c.IssueId), sprintId, notify },
            ActorId: actorId), ct);

        if (notify)
        {
            foreach (var c in changed)
            {
                try { await notifications.OnIssueSprintChangedAsync(c.IssueId, c.Previous, sprintId, actorId, ct); }
                catch { /* best-effort */ }
            }
        }
        return changed.Count;
    }

    public async Task<int> BulkAddTagAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        string tag, Guid actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag is required.", nameof(tag));
        var trimmed = tag.Trim();
        var hits = await LoadInProjectAsync(projectId, issueIds, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        var touched = 0;
        foreach (var i in hits)
        {
            // SetLabels already case-insensitive-dedupes; only count issues
            // that didn't already carry the tag so the audit number matches
            // intuition.
            if (i.Labels.Any(l => string.Equals(l, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
            i.SetLabels(i.Labels.Append(trimmed), now);
            touched++;
        }
        if (touched == 0) return 0;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "project.issues.bulk_tag_added",
            ResourceType: "Project",
            ResourceId: projectId.ToString(),
            Summary: $"Bulk added tag '{trimmed}' to {touched} issue(s)",
            Detail: new { issueIds = hits.Select(h => h.Id), tag = trimmed },
            ActorId: actorId), ct);
        return touched;
    }

    public async Task<int> BulkRemoveTagAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        string tag, Guid actorId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag is required.", nameof(tag));
        var trimmed = tag.Trim();
        var hits = await LoadInProjectAsync(projectId, issueIds, ct);
        if (hits.Count == 0) return 0;

        var now = clock.UtcNow;
        var touched = 0;
        foreach (var i in hits)
        {
            if (!i.Labels.Any(l => string.Equals(l, trimmed, StringComparison.OrdinalIgnoreCase))) continue;
            var rebuilt = i.Labels.Where(l => !string.Equals(l, trimmed, StringComparison.OrdinalIgnoreCase));
            i.SetLabels(rebuilt, now);
            touched++;
        }
        if (touched == 0) return 0;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "project.issues.bulk_tag_removed",
            ResourceType: "Project",
            ResourceId: projectId.ToString(),
            Summary: $"Bulk removed tag '{trimmed}' from {touched} issue(s)",
            Detail: new { issueIds = hits.Select(h => h.Id), tag = trimmed },
            ActorId: actorId), ct);
        return touched;
    }

    public async Task<BulkTransitionResult> BulkTransitionAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        Guid targetStateId, Guid actorId, bool notify, CancellationToken ct)
    {
        var workflow = await workflows.GetForProjectAsync(projectId, ct)
            ?? throw new InvalidOperationException("Project has no workflow.");
        var target = workflow.States.FirstOrDefault(s => s.Id == targetStateId)
            ?? throw new InvalidOperationException("Target state isn't in this project's workflow.");

        var hits = await LoadInProjectAsync(projectId, issueIds, ct);
        if (hits.Count == 0)
            return new BulkTransitionResult(0, Array.Empty<BulkTransitionFailure>());

        var now = clock.UtcNow;
        var succeeded = new List<Guid>();
        var failed = new List<BulkTransitionFailure>();
        foreach (var i in hits)
        {
            if (i.WorkflowStateId == targetStateId)
            {
                // Already there — counts as success without an UpdatedAt bump.
                continue;
            }
            if (!workflow.IsTransitionAllowed(i.WorkflowStateId, targetStateId))
            {
                var fromName = workflow.States.FirstOrDefault(s => s.Id == i.WorkflowStateId)?.Name ?? "?";
                failed.Add(new BulkTransitionFailure(i.Id,
                    $"Workflow forbids \"{fromName}\" → \"{target.Name}\""));
                continue;
            }
            i.Transition(targetStateId, now);
            if (target.Category == WorkflowStateCategory.Done) i.MarkComplete(now);
            succeeded.Add(i.Id);
        }
        if (succeeded.Count > 0) await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "project.issues.bulk_transitioned",
            ResourceType: "Project",
            ResourceId: projectId.ToString(),
            Summary: $"Bulk transitioned {succeeded.Count} issue(s) to '{target.Name}' " +
                     (failed.Count > 0 ? $"({failed.Count} rejected by workflow)" : ""),
            Detail: new { issueIds = succeeded, targetStateId, failedCount = failed.Count, notify },
            ActorId: actorId), ct);

        if (notify && target.Category == WorkflowStateCategory.Done)
        {
            foreach (var id in succeeded)
            {
                try { await notifications.OnIssueTransitionedAsync(id, true, actorId, ct); }
                catch { /* best-effort */ }
            }
        }
        return new BulkTransitionResult(succeeded.Count, failed);
    }

    private Task<List<Issue>> LoadInProjectAsync(Guid projectId, IReadOnlyCollection<Guid> issueIds,
        CancellationToken ct)
    {
        // Defense-in-depth: only load issues that belong to the target project,
        // even if the caller's UI pushed in an ID from elsewhere.
        return db.Set<Issue>()
            .Where(i => i.ProjectId == projectId && issueIds.Contains(i.Id))
            .ToListAsync(ct);
    }
}
