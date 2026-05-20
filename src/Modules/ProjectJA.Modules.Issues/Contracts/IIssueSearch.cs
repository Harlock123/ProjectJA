// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Workflows.Domain;

namespace ProjectJA.Modules.Issues.Contracts;

public sealed record IssueSearchHit(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Title,
    string? Description,
    Guid WorkflowStateId,
    string WorkflowStateName,
    WorkflowStateCategory WorkflowStateCategory,
    double Rank);

public interface IIssueSearch
{
    Task<IReadOnlyList<IssueSearchHit>> SearchAsync(string query, int limit, CancellationToken ct);
}
