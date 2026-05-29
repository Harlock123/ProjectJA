// SPDX-License-Identifier: BUSL-1.1
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Issues.Contracts;
using ProjectJA.Modules.Issues.Domain;
using ProjectJA.Modules.Projects.Domain;
using ProjectJA.Modules.Workflows.Contracts;
using ProjectJA.Modules.Workflows.Domain;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Issues.Application;

internal sealed class IssueImportService(
    DbContext db,
    IWorkflowQueries workflows,
    IClock clock) : IIssueImportService
{
    // Hard caps to keep a runaway sheet from locking the circuit / blowing
    // request memory. Matches the user's stated expectations from the design
    // discussion (~373 rows in the sample, room for ~5x growth).
    private const int MaxRows = 2000;

    // Column headers we look for, case-insensitive. Required = WBS or Task Name.
    private static readonly string[] WbsHeaders = ["WBS", "Outline Number", "Outline"];
    private static readonly string[] TitleHeaders = ["Task Name", "Name", "Title"];
    private static readonly string[] StartHeaders = ["Start", "Start Date"];
    private static readonly string[] FinishHeaders = ["Finish", "End", "End Date", "Due"];
    private static readonly string[] PercentHeaders = ["% Complete", "Percent Complete", "% Done"];
    private static readonly string[] NotesHeaders = ["Notes", "Note", "Comment", "Comments"];

    public ImportPlan Parse(Stream xlsx)
    {
        XLWorkbook book;
        try { book = new XLWorkbook(xlsx); }
        catch (Exception ex) { throw new InvalidDataException("Couldn't open the file as an xlsx workbook.", ex); }

        var sheet = book.Worksheets.FirstOrDefault()
            ?? throw new InvalidDataException("Workbook contains no worksheets.");

        var headerRow = sheet.FirstRowUsed()
            ?? throw new InvalidDataException("Worksheet is empty.");
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in headerRow.CellsUsed())
            headers[c.GetString().Trim()] = c.Address.ColumnNumber;

        int Col(string[] options) =>
            options.Select(o => headers.TryGetValue(o, out var n) ? n : 0).FirstOrDefault(n => n > 0);

        var wbsCol = Col(WbsHeaders);
        var titleCol = Col(TitleHeaders);
        if (wbsCol == 0 && titleCol == 0)
            throw new InvalidDataException(
                "Couldn't find a WBS or Task Name column in row 1. Expected an MS-Project-style header.");

        var startCol = Col(StartHeaders);
        var finishCol = Col(FinishHeaders);
        var percentCol = Col(PercentHeaders);
        var notesCol = Col(NotesHeaders);

        var planned = new List<PlannedIssue>();
        var warnings = new List<string>();
        var seenWbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var dataStart = headerRow.RowNumber() + 1;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = dataStart; r <= lastRow; r++)
        {
            if (planned.Count >= MaxRows)
            {
                warnings.Add($"Stopped at row {r}: import is capped at {MaxRows} rows.");
                break;
            }
            var row = sheet.Row(r);
            var wbs = wbsCol > 0 ? NormalizeWbs(row.Cell(wbsCol)) : null;
            var title = titleCol > 0 ? row.Cell(titleCol).GetString().Trim() : null;
            if (string.IsNullOrEmpty(wbs) && string.IsNullOrEmpty(title)) continue; // blank row

            // We need both a stable identity (WBS) and a name to create an issue.
            if (string.IsNullOrEmpty(title))
            {
                warnings.Add($"Row {r}: skipped — no Task Name.");
                continue;
            }
            if (string.IsNullOrEmpty(wbs))
            {
                // Synthesise a flat WBS so the row still imports (no parent link).
                wbs = $"row-{r}";
            }
            if (!seenWbs.Add(wbs))
            {
                warnings.Add($"Row {r}: WBS '{wbs}' appears more than once — only the first row will own the parent link.");
            }

            var (depth, parent) = WbsHierarchy(wbs);
            var suggestedType = depth <= 1 ? IssueType.Epic
                              : depth == 2 ? IssueType.Story
                              : IssueType.Task;

            DateTimeOffset? start = null, end = null;
            if (startCol > 0)
                start = ReadDate(row.Cell(startCol), r, "Start", warnings);
            if (finishCol > 0)
                end = ReadDate(row.Cell(finishCol), r, "Finish", warnings);
            if (start is { } sd && end is { } ed && ed < sd)
            {
                warnings.Add($"Row {r}: Finish ({ed:yyyy-MM-dd}) precedes Start ({sd:yyyy-MM-dd}); dropping Finish.");
                end = null;
            }

            var percent = 0;
            if (percentCol > 0)
            {
                if (row.Cell(percentCol).TryGetValue<double>(out var p) && double.IsFinite(p))
                {
                    // Excel "% Complete" cells are 0–1 fractions; Project's export
                    // matches. Anything ≥ 2 we treat as an already-percent integer
                    // so a hand-edited "50" still imports cleanly.
                    var asPct = p <= 1.0 + 1e-9 ? p * 100.0 : p;
                    percent = (int)Math.Round(Math.Clamp(asPct, 0, 100));
                }
            }

            string? note = null;
            if (notesCol > 0)
            {
                var n = row.Cell(notesCol).GetString()?.Trim();
                if (!string.IsNullOrEmpty(n)) note = n;
            }

            planned.Add(new PlannedIssue(
                SourceRow: r,
                Wbs: wbs,
                ParentWbs: parent,
                Depth: depth,
                Title: title,
                Description: title,           // Per request: column C is both short and long description.
                StartDate: start,
                EndDate: end,
                PercentComplete: percent,
                Note: note,
                SuggestedType: suggestedType));
        }

        return new ImportPlan(planned, warnings);
    }

    public async Task<ImportResult> ApplyAsync(Guid projectId, ImportPlan plan, Guid actorId, CancellationToken ct)
    {
        var workflow = await workflows.GetForProjectAsync(projectId, ct)
            ?? throw new InvalidOperationException("Project has no workflow configured.");
        var initialStateId = workflow.States.OrderBy(s => s.Order)
            .FirstOrDefault(s => s.Category == WorkflowStateCategory.Open)?.Id
            ?? workflow.States.OrderBy(s => s.Order).First().Id;
        var doneStateId = workflow.States.OrderBy(s => s.Order)
            .FirstOrDefault(s => s.Category == WorkflowStateCategory.Done)?.Id;

        var project = await db.Set<Project>().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException("Project not found.");

        var now = clock.UtcNow;
        var batchLabel = $"import:{now:yyyyMMdd-HHmmss}";
        var warnings = new List<string>(plan.Warnings);
        var byWbs = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var createdIds = new List<Guid>(plan.Issues.Count);
        var commentsAdded = 0;

        // Phase 1 — create issues. AllocateIssueNumber mutates the tracked
        // Project, so EF persists NextIssueNumber alongside the inserts.
        foreach (var p in plan.Issues)
        {
            var number = project.AllocateIssueNumber();
            var issue = Issue.Create(projectId, number, p.Title, p.Description, initialStateId, actorId, now);
            issue.Reclassify(p.SuggestedType, points: null, acceptanceCriteria: null, now);
            issue.SetLabels(new[] { $"wbs:{p.Wbs}", batchLabel }, now);

            try { issue.SetSchedule(p.StartDate, p.EndDate, now); }
            catch (InvalidOperationException ex) { warnings.Add($"WBS {p.Wbs}: {ex.Message}"); }

            try { issue.SetPercentComplete(p.PercentComplete, now); }
            catch (InvalidOperationException ex) { warnings.Add($"WBS {p.Wbs}: {ex.Message}"); }

            if (p.PercentComplete >= 100 && doneStateId is { } ds)
            {
                issue.Transition(ds, now);
                issue.MarkComplete(now);
            }

            if (!string.IsNullOrWhiteSpace(p.Note))
            {
                issue.AddComment(actorId, p.Note!, now);
                commentsAdded++;
            }

            db.Set<Issue>().Add(issue);
            // First-write wins if the sheet repeats a WBS — matches the
            // warning emitted in Parse.
            byWbs.TryAdd(p.Wbs, issue.Id);
            createdIds.Add(issue.Id);
        }

        await db.SaveChangesAsync(ct);

        // Phase 2 — wire parent blocker links. Per the spec: a child WBS like
        // 1.1.1 BLOCKS its parent 1.1 (parent can't be done until children are).
        // Dedupe by (childId, parentId): when the sheet repeats a WBS row, both
        // rows map to the same child issue (first-write-wins on byWbs), so the
        // naive loop would try to insert the same link twice and trip the
        // unique index on (BlockerIssueId, BlockedIssueId).
        var linksCreated = 0;
        var seenLinks = new HashSet<(Guid Child, Guid Parent)>();
        foreach (var p in plan.Issues)
        {
            if (p.ParentWbs is null) continue;
            if (!byWbs.TryGetValue(p.Wbs, out var childId)) continue;
            if (!byWbs.TryGetValue(p.ParentWbs, out var parentId))
            {
                warnings.Add($"WBS {p.Wbs}: parent '{p.ParentWbs}' not present in the import; blocker skipped.");
                continue;
            }
            if (!seenLinks.Add((childId, parentId))) continue;
            db.Set<IssueLink>().Add(IssueLink.Create(
                blockerIssueId: childId, blockedIssueId: parentId, actorId, now));
            linksCreated++;
        }

        if (linksCreated > 0)
            await db.SaveChangesAsync(ct);

        return new ImportResult(
            IssuesCreated: createdIds.Count,
            LinksCreated: linksCreated,
            CommentsAdded: commentsAdded,
            BatchLabel: batchLabel,
            Warnings: warnings,
            CreatedIssueIds: createdIds);
    }

    /// <summary>Extracts a clean WBS string from a cell. Handles Excel's
    /// float-noise representation of single-decimal numbers (e.g. an Excel
    /// "1.1" coming back as 1.1000000000000001) and trims whitespace.</summary>
    private static string? NormalizeWbs(IXLCell cell)
    {
        if (cell.Value.IsBlank) return null;
        // For a TEXT cell ClosedXML returns the typed string as-is.
        if (cell.Value.IsText)
        {
            var s = cell.GetString().Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }
        // For a NUMBER cell (top- and second-level WBS rows), the displayed
        // text may carry binary-float noise. Round-trip via decimal so "1.1"
        // comes back cleanly and "2" stays "2".
        if (cell.TryGetValue<double>(out var d) && double.IsFinite(d))
        {
            var rounded = Math.Round((decimal)d, 4, MidpointRounding.AwayFromZero);
            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }
        var fallback = cell.GetFormattedString()?.Trim();
        return string.IsNullOrEmpty(fallback) ? null : fallback;
    }

    /// <summary>WBS depth and immediate parent. "1" → (1, null); "1.2" → (2, "1");
    /// "1.2.3" → (3, "1.2"). Non-dotted strings ("row-7") are depth 1, no parent.</summary>
    private static (int Depth, string? Parent) WbsHierarchy(string wbs)
    {
        var lastDot = wbs.LastIndexOf('.');
        if (lastDot < 0) return (1, null);
        var depth = wbs.Count(c => c == '.') + 1;
        return (depth, wbs[..lastDot]);
    }

    private static DateTimeOffset? ReadDate(IXLCell cell, int row, string label, List<string> warnings)
    {
        if (cell.Value.IsBlank) return null;
        try
        {
            // Numeric/DateTime cells: ClosedXML converts the Excel serial.
            var dt = cell.GetDateTime();
            return new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc), TimeSpan.Zero);
        }
        catch
        {
            // String-typed cells (rare for date columns but possible): try a
            // permissive parse, then give up with a warning.
            var s = cell.GetString().Trim();
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                return new DateTimeOffset(DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc), TimeSpan.Zero);
            warnings.Add($"Row {row}: couldn't parse {label} value '{s}' as a date — left blank.");
            return null;
        }
    }
}
