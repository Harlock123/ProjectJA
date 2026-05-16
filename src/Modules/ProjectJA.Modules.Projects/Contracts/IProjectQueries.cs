// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Contracts;

public interface IProjectQueries
{
    Task<ProjectSummary?> GetSummaryAsync(Guid projectId, CancellationToken ct);
    Task<int?> AllocateNextIssueNumberAsync(Guid projectId, CancellationToken ct);
}
