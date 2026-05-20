// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Workflows.Contracts;

/// <summary>Cross-module entry-point the Projects module uses to make sure a
/// new project gets its default workflow created. Idempotent — calling it
/// twice for the same project is a no-op. Lives in Workflows.Contracts so
/// Projects.csproj can reference it without Workflows.csproj needing to
/// reference Projects (one-way dependency Projects → Workflows).</summary>
public interface IWorkflowSeeder
{
    /// <summary>Create the system "Default" workflow (Todo/Doing/Done states
    /// with Open/InProgress/Done categories) for this project, unless one
    /// already exists. Returns the workflow id either way.</summary>
    Task<Guid> EnsureDefaultForProjectAsync(Guid projectId, DateTimeOffset now, CancellationToken ct);
}
