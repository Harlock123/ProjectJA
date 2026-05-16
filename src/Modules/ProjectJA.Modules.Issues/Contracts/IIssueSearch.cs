// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Issues.Domain;

namespace ProjectJA.Modules.Issues.Contracts;

public sealed record IssueSearchHit(
    Guid Id,
    Guid ProjectId,
    string ProjectKey,
    int Number,
    string Title,
    string? Description,
    IssueStatus Status,
    double Rank);

public interface IIssueSearch
{
    Task<IReadOnlyList<IssueSearchHit>> SearchAsync(string query, int limit, CancellationToken ct);
}
