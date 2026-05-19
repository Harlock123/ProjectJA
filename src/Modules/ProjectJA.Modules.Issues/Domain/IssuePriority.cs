// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

/// <summary>Relative urgency of an issue. Stored as int.</summary>
public enum IssuePriority
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
