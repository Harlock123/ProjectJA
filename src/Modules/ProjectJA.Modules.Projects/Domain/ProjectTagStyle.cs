// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

/// <summary>Persistent style for a single tag value within a project. The tag
/// itself lives on <see cref="ProjectJA.Modules.Issues.Domain.Issue"/>.Labels
/// (free-text Postgres text[]); this row is a side-table that maps a tag name
/// to its rendering color. Foreground text is computed at render time from
/// the background's luminance — we only store the background.</summary>
public sealed class ProjectTagStyle
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Tag { get; private set; } = default!;
    public string BackgroundHex { get; private set; } = default!;
    public DateTimeOffset UpdatedAt { get; private set; }

    private ProjectTagStyle() { }

    public ProjectTagStyle(Guid id, Guid projectId, string tag, string backgroundHex, DateTimeOffset now)
    {
        Id = id;
        ProjectId = projectId;
        Tag = tag;
        BackgroundHex = backgroundHex;
        UpdatedAt = now;
    }

    public static ProjectTagStyle Create(Guid projectId, string tag, string backgroundHex, DateTimeOffset now)
        => new(Guid.NewGuid(), projectId, tag, backgroundHex, now);

    public void SetBackground(string backgroundHex, DateTimeOffset now)
    {
        BackgroundHex = backgroundHex;
        UpdatedAt = now;
    }

    /// <summary>Apply a tag-rename so the style follows.</summary>
    public void Rename(string newTag, DateTimeOffset now)
    {
        Tag = newTag;
        UpdatedAt = now;
    }
}
