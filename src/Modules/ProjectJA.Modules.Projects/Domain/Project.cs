// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Projects.Domain;

public sealed class Project
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public int NextIssueNumber { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Project() { }

    public Project(Guid id, Guid organizationId, string key, string name, string? description, DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Key = key.ToUpperInvariant();
        Name = name;
        Description = description;
        NextIssueNumber = 1;
        CreatedAt = createdAt;
    }

    public static Project Create(Guid organizationId, string key, string name, string? description, DateTimeOffset now)
        => new(Guid.NewGuid(), organizationId, key, name, description, now);

    public int AllocateIssueNumber()
    {
        var n = NextIssueNumber;
        NextIssueNumber = n + 1;
        return n;
    }

    public void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
