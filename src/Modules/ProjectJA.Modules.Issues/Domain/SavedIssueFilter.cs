// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Issues.Domain;

/// <summary>One named, per-(user, project) filter on the ProjectDetail issues
/// table. The filter shape itself lives in the UI layer (Razor-side
/// <c>IssueFilterDialog.IssueFilter</c>); this entity persists it as opaque
/// JSON so the storage doesn't need to migrate every time the UI gains a
/// dimension.</summary>
public sealed class SavedIssueFilter
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = default!;
    public string FilterJson { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SavedIssueFilter() { }

    public SavedIssueFilter(Guid id, Guid userId, Guid projectId, string name, string filterJson, DateTimeOffset now)
    {
        Id = id;
        UserId = userId;
        ProjectId = projectId;
        Name = name;
        FilterJson = filterJson;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static SavedIssueFilter Create(Guid userId, Guid projectId, string name, string filterJson, DateTimeOffset now)
        => new(Guid.NewGuid(), userId, projectId, name, filterJson, now);

    public void Replace(string filterJson, DateTimeOffset now)
    {
        FilterJson = filterJson;
        UpdatedAt = now;
    }
}
