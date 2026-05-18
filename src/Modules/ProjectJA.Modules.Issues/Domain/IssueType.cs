// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

/// <summary>Agile/Scrum work-item classification. Stored as int.</summary>
public enum IssueType
{
    Story = 0,
    Task = 1,
    Spike = 2,
    Enhancement = 3,
    Defect = 4,
    Chore = 5,
    Epic = 6
}
