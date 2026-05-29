// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>One row from an MS-Project-style xlsx, normalised into a shape the
/// importer can act on. Built by <see cref="IIssueImportService.Parse"/>;
/// consumed by <see cref="IIssueImportService.ApplyAsync"/>. Round-trips
/// safely through JSON / SignalR so the preview UI can display it before the
/// user commits.</summary>
public sealed record PlannedIssue(
    int SourceRow,
    string Wbs,
    string? ParentWbs,
    int Depth,
    string Title,
    string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    int PercentComplete,
    string? Note,
    IssueType SuggestedType);

public sealed record ImportPlan(
    IReadOnlyList<PlannedIssue> Issues,
    IReadOnlyList<string> Warnings)
{
    public int RowCount => Issues.Count;
    public int LinkCount => Issues.Count(i => i.ParentWbs is not null);
    public int CompletedCount => Issues.Count(i => i.PercentComplete >= 100);
    public int DatedCount => Issues.Count(i => i.StartDate is not null || i.EndDate is not null);
    public int CommentCount => Issues.Count(i => !string.IsNullOrWhiteSpace(i.Note));
}

public sealed record ImportResult(
    int IssuesCreated,
    int LinksCreated,
    int CommentsAdded,
    string BatchLabel,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<Guid> CreatedIssueIds);
