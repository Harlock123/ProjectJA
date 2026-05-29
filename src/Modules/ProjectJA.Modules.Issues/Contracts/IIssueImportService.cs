// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Contracts;

public interface IIssueImportService
{
    /// <summary>Parses an MS-Project-style xlsx stream into a plan. Does NOT
    /// touch the database. Throws <see cref="InvalidDataException"/> for an
    /// unreadable file or missing required header columns; per-row problems
    /// are returned as warnings on the plan instead of throwing so the UI
    /// can present them.</summary>
    ImportPlan Parse(Stream xlsx);

    /// <summary>Materialise the plan into a project as new issues + parent
    /// blocker links + comments. Caller has already verified the actor is an
    /// Admin on <paramref name="projectId"/> and that the project's workflow
    /// has been seeded.</summary>
    Task<ImportResult> ApplyAsync(Guid projectId, ImportPlan plan, Guid actorId, CancellationToken ct);
}
