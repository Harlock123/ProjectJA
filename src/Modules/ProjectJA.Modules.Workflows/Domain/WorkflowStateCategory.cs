// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Workflows.Domain;

/// <summary>The semantic bucket a state falls into. Lets non-status-specific
/// logic (sprint completion's "is this issue still open?", reporting roll-ups,
/// board column tinting) ask one question instead of enumerating every project's
/// custom state names. Three categories cover the standard lifecycle and match
/// the historic <c>IssueStatus</c> enum (Todo→Open, Doing→InProgress, Done→Done)
/// that this model replaces.</summary>
public enum WorkflowStateCategory
{
    Open = 0,
    InProgress = 1,
    Done = 2
}
