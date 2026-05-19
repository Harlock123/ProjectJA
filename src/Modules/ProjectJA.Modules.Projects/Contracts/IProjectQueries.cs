// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.Modules.Projects.Domain;

namespace ProjectJA.Modules.Projects.Contracts;

public sealed record ProjectMemberInfo(Guid UserId, ProjectRole Role, DateTimeOffset AddedAt);

public interface IProjectQueries
{
    Task<ProjectSummary?> GetSummaryAsync(Guid projectId, CancellationToken ct);
    Task<int?> AllocateNextIssueNumberAsync(Guid projectId, CancellationToken ct);

    /// <summary>True if the user has any role in the project.</summary>
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken ct);

    /// <summary>The user's role in the project, or null if not a member.</summary>
    Task<ProjectRole?> GetRoleAsync(Guid projectId, Guid userId, CancellationToken ct);

    Task<IReadOnlyList<ProjectMemberInfo>> ListMembersAsync(Guid projectId, CancellationToken ct);
}
