// SPDX-License-Identifier: BUSL-1.1
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Contracts;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class IssueExportService(
    DbContext db,
    IProjectQueries projects,
    IWorkflowQueries workflows,
    IUserQueries users) : IIssueExportService
{
    public Task<byte[]?> ExportProjectAsync(Guid projectId, CancellationToken ct)
        => ExportProjectAsync(projectId, filter: null, ct);

    public async Task<byte[]?> ExportProjectAsync(Guid projectId, IssueExportFilter? filter, CancellationToken ct)
    {
        var project = await projects.GetSummaryAsync(projectId, ct);
        if (project is null) return null;

        var query = db.Set<Issue>().AsNoTracking()
            .Where(i => i.ProjectId == projectId);

        if (filter is not null)
        {
            if (filter.Type is { } t) query = query.Where(i => i.Type == t);
            if (filter.WorkflowStateId is { } s) query = query.Where(i => i.WorkflowStateId == s);
            if (filter.Priority is { } p) query = query.Where(i => i.Priority == p);
            if (filter.AssigneeId is { } a)
            {
                query = a == Guid.Empty
                    ? query.Where(i => i.AssigneeId == null)
                    : query.Where(i => i.AssigneeId == a);
            }
            if (!string.IsNullOrWhiteSpace(filter.TitleContains))
            {
                var needle = filter.TitleContains!.Trim();
                // EF.Functions.ILike keeps the case-insensitive match server-side
                // on Postgres; mirrors the in-page filter's OrdinalIgnoreCase.
                query = query.Where(i => EF.Functions.ILike(i.Title, $"%{needle}%"));
            }
            if (filter.Tags is { Count: > 0 } tagList)
            {
                // AND across selected tags — Postgres text[] containment via
                // Contains. Case sensitivity matches the UI filter (which is
                // also case-sensitive on stored values; SetLabels canonicalises
                // first-casing-wins, so a project carries one casing per tag).
                foreach (var tag in tagList)
                {
                    var captured = tag;
                    query = query.Where(i => i.Labels.Contains(captured));
                }
            }
        }

        var issues = await query.OrderBy(i => i.Number).ToListAsync(ct);

        var workflow = await workflows.GetForProjectAsync(projectId, ct);
        var stateNames = workflow?.States.ToDictionary(s => s.Id, s => s.Name)
                         ?? new Dictionary<Guid, string>();

        var assigneeIds = issues.Where(i => i.AssigneeId.HasValue)
            .Select(i => i.AssigneeId!.Value).Distinct().ToList();
        var userMap = assigneeIds.Count == 0
            ? new Dictionary<Guid, UserSummary>()
            : (Dictionary<Guid, UserSummary>)await users.GetSummariesAsync(assigneeIds, ct);

        // Build a Number lookup once, then resolve each issue's blockers to
        // their human "KEY-N" strings for the Predecessors column.
        var numberByIssue = issues.ToDictionary(i => i.Id, i => i.Number);
        var issueIds = numberByIssue.Keys.ToList();
        var links = await db.Set<IssueLink>().AsNoTracking()
            .Where(l => issueIds.Contains(l.BlockedIssueId))
            .Select(l => new { l.BlockedIssueId, l.BlockerIssueId })
            .ToListAsync(ct);
        var blockersFor = links
            .GroupBy(l => l.BlockedIssueId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => numberByIssue.TryGetValue(x.BlockerIssueId, out var n)
                                    ? $"{project.Key}-{n}"
                                    : null)
                      .Where(s => s is not null)
                      .OrderBy(s => s, StringComparer.Ordinal)
                      .ToList());

        using var workbook = new XLWorkbook();
        var sheetName = SanitizeSheetName(project.Key);
        var sheet = workbook.Worksheets.Add(sheetName);

        // Header — kept close to MS Project's default Gantt-table columns.
        var headers = new[] { "ID", "Title", "Status", "Assignee", "Start", "Finish", "% Complete", "Predecessors" };
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var row = 2;
        foreach (var issue in issues)
        {
            sheet.Cell(row, 1).Value = $"{project.Key}-{issue.Number}";
            sheet.Cell(row, 2).Value = issue.Title;
            sheet.Cell(row, 3).Value = stateNames.GetValueOrDefault(issue.WorkflowStateId, "—");
            sheet.Cell(row, 4).Value = issue.AssigneeId is { } aid && userMap.TryGetValue(aid, out var u)
                ? u.DisplayName : "";
            if (issue.StartDate is { } sd) sheet.Cell(row, 5).Value = sd.LocalDateTime.Date;
            if (issue.EndDate is { } ed) sheet.Cell(row, 6).Value = ed.LocalDateTime.Date;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 6).Style.NumberFormat.Format = "yyyy-mm-dd";
            sheet.Cell(row, 7).Value = issue.PercentComplete / 100.0;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "0%";
            sheet.Cell(row, 8).Value = blockersFor.TryGetValue(issue.Id, out var preds)
                ? string.Join(", ", preds!) : "";
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // Excel sheet names: max 31 chars, can't contain : \ / ? * [ ].
    private static string SanitizeSheetName(string raw)
    {
        var cleaned = new string((raw ?? "Project")
            .Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']'))
            .ToArray());
        if (string.IsNullOrWhiteSpace(cleaned)) cleaned = "Project";
        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
