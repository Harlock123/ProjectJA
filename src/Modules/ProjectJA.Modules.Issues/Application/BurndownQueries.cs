// SPDX-License-Identifier: BUSL-1.1
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Audit.Domain;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class BurndownQueries(DbContext db, IClock clock, IWorkflowQueries workflows) : IBurndownQueries
{
    public async Task<BurndownView?> GetForSprintAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await db.Set<Sprint>().AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sprintId, ct);
        if (sprint is null) return null;
        if (sprint.StartedAt is null) return null;     // Planned sprint — nothing to burn down.

        var todayUtc = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var start = DateOnly.FromDateTime(sprint.StartedAt.Value.UtcDateTime);
        var end = sprint.CompletedAt is { } completed
            ? DateOnly.FromDateTime(completed.UtcDateTime)
            : MaxDate(todayUtc, sprint.PlannedEnd is { } planned
                ? DateOnly.FromDateTime(planned.UtcDateTime) : todayUtc);
        if (end < start) end = start;                  // pathological — collapse to a single day.

        // Issues currently in the sprint. Scope changes mid-sprint aren't
        // reconstructed in v1; this matches the rest of the Sprints UI.
        var issues = await db.Set<Issue>().AsNoTracking()
            .Where(i => i.SprintId == sprintId)
            .Select(i => new IssueRow(i.Id, i.ProjectId, i.WorkflowStateId, i.Points))
            .ToListAsync(ct);
        if (issues.Count == 0) return null;

        // Load the project's workflow state categories (every sprint's issues
        // share one project, so a single lookup is fine).
        var projectId = issues[0].ProjectId;
        var workflow = await workflows.GetForProjectAsync(projectId, ct);
        var categories = workflow?.States.ToDictionary(s => s.Id, s => s.Category)
            ?? new Dictionary<Guid, WorkflowStateCategory>();

        // Which currently-Done issues are we accounting for? An issue is Done
        // now iff its WorkflowStateId is in a Done-category state. For each
        // such issue, the LATEST 'issue.transitioned' event in audit (within
        // the sprint window) is its Done timestamp — correct because a
        // Done→non-Done bounce would mean current state isn't Done.
        var doneIssueIds = issues
            .Where(i => categories.TryGetValue(i.WorkflowStateId, out var c) && c == WorkflowStateCategory.Done)
            .Select(i => i.Id)
            .ToList();

        var doneByIssue = new Dictionary<Guid, DateOnly>();
        if (doneIssueIds.Count > 0)
        {
            var idStrings = doneIssueIds.Select(g => g.ToString()).ToList();
            var sprintStartTs = sprint.StartedAt.Value;
            var events = await db.Set<AuditEvent>().AsNoTracking()
                .Where(e => e.Action == "issue.transitioned"
                            && e.OccurredAt >= sprintStartTs
                            && idStrings.Contains(e.ResourceId))
                .GroupBy(e => e.ResourceId)
                .Select(g => new { ResourceId = g.Key, LastAt = g.Max(e => e.OccurredAt) })
                .ToListAsync(ct);
            foreach (var row in events)
            {
                if (Guid.TryParse(row.ResourceId, out var issueGuid))
                    doneByIssue[issueGuid] = DateOnly.FromDateTime(row.LastAt.UtcDateTime);
            }
        }

        var totalIssues = issues.Count;
        var totalPoints = issues.Sum(i => i.Points ?? 0);

        // Walk the day axis, subtracting work as each Done timestamp falls
        // due. The ideal line is a straight slope from total → 0.
        var days = new List<BurndownDay>();
        var totalDays = end.DayNumber - start.DayNumber;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var progress = totalDays == 0 ? 1.0 : (double)(d.DayNumber - start.DayNumber) / totalDays;
            var idealCount = totalIssues * (1 - progress);
            var idealPoints = totalPoints * (1 - progress);

            var burnedIssueIds = doneByIssue.Where(kvp => kvp.Value <= d).Select(kvp => kvp.Key).ToHashSet();
            var actualCount = totalIssues - burnedIssueIds.Count;
            var actualPoints = totalPoints - issues
                .Where(i => burnedIssueIds.Contains(i.Id))
                .Sum(i => i.Points ?? 0);

            days.Add(new BurndownDay(d, idealCount, actualCount, idealPoints, actualPoints));
        }

        return new BurndownView(
            SprintId: sprint.Id,
            SprintName: sprint.Name,
            Start: start,
            End: end,
            Today: todayUtc,
            TotalIssues: totalIssues,
            TotalPoints: totalPoints,
            Days: days);
    }

    private static DateOnly MaxDate(DateOnly a, DateOnly b) => a > b ? a : b;

    private sealed record IssueRow(Guid Id, Guid ProjectId, Guid WorkflowStateId, int? Points);
}
