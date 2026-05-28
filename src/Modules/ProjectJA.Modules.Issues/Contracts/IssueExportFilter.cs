// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Contracts;

/// <summary>Optional restriction set used when exporting a project to xlsx.
/// Mirrors the in-page filter on ProjectDetail. A null property means "don't
/// filter on this dimension"; <see cref="AssigneeId"/> == Guid.Empty is the
/// "unassigned" sentinel.</summary>
public sealed record IssueExportFilter(
    IssueType? Type,
    Guid? WorkflowStateId,
    IssuePriority? Priority,
    Guid? AssigneeId,
    string? TitleContains)
{
    public bool IsActive =>
        Type is not null
        || WorkflowStateId is not null
        || Priority is not null
        || AssigneeId is not null
        || !string.IsNullOrWhiteSpace(TitleContains);
}
