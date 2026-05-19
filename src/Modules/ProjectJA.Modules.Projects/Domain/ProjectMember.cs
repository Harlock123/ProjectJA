// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>Membership of a user in a project. Owned by the Project aggregate;
/// keyed by (ProjectId, UserId). UserId is an Identity user id (no cross-module
/// FK — consistent with Issue.AssigneeId).</summary>
public sealed class ProjectMember
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectRole Role { get; private set; }
    public DateTimeOffset AddedAt { get; private set; }

    private ProjectMember() { }

    internal ProjectMember(Guid projectId, Guid userId, ProjectRole role, DateTimeOffset addedAt)
    {
        ProjectId = projectId;
        UserId = userId;
        Role = role;
        AddedAt = addedAt;
    }

    internal void SetRole(ProjectRole role) => Role = role;
}
