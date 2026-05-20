// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>Lifecycle of a sprint. Planned → Active → Completed; transitions
/// are one-way (no reopening for now). At most one Active sprint per project
/// — enforced at the endpoint layer; the domain just gates the transition.</summary>
public enum SprintStatus
{
    Planned = 0,
    Active = 1,
    Completed = 2
}
