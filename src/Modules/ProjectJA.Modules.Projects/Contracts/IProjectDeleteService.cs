// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Contracts;

public interface IProjectDeleteService
{
    /// <summary>Hard-delete a project and everything project-scoped: issues,
    /// owned issue comments, attachment metadata, blocker links, sprints, and
    /// the project's workflow + states + transitions. Notifications that
    /// reference deleted issues are intentionally left in place — they stay
    /// readable as user history, just with dead links.</summary>
    /// <returns>True if the project existed and was deleted; false if no row
    /// matched the id.</returns>
    Task<bool> DeleteAsync(Guid projectId, CancellationToken ct);
}
