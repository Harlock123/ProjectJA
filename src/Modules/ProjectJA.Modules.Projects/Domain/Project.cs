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

    private readonly List<ProjectMember> _members = new();
    public IReadOnlyList<ProjectMember> Members => _members;

    private Project() { }

    public Project(Guid id, Guid organizationId, string key, string name, string? description,
        Guid createdByUserId, DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Key = key.ToUpperInvariant();
        Name = name;
        Description = description;
        NextIssueNumber = 1;
        CreatedAt = createdAt;
        // The creator is the first Admin.
        _members.Add(new ProjectMember(id, createdByUserId, ProjectRole.Admin, createdAt));
    }

    public static Project Create(Guid organizationId, string key, string name, string? description,
        Guid createdByUserId, DateTimeOffset now)
        => new(Guid.NewGuid(), organizationId, key, name, description, createdByUserId, now);

    public ProjectMember AddMember(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new InvalidOperationException("User is already a member of this project.");
        var member = new ProjectMember(Id, userId, role, now);
        _members.Add(member);
        return member;
    }

    public void RemoveMember(Guid userId, DateTimeOffset now)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("User is not a member of this project.");
        if (member.Role == ProjectRole.Admin && _members.Count(m => m.Role == ProjectRole.Admin) == 1)
            throw new InvalidOperationException("Cannot remove the only Admin of the project.");
        _members.Remove(member);
    }

    public void ChangeMemberRole(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        var member = _members.FirstOrDefault(m => m.UserId == userId)
            ?? throw new InvalidOperationException("User is not a member of this project.");
        if (member.Role == ProjectRole.Admin && role != ProjectRole.Admin
            && _members.Count(m => m.Role == ProjectRole.Admin) == 1)
            throw new InvalidOperationException("Cannot demote the only Admin of the project.");
        member.SetRole(role);
    }

    /// <summary>Backfill for legacy projects created before membership existed:
    /// adds only if absent (no last-Admin checks). Returns the new member, or
    /// null if the user was already a member.</summary>
    public ProjectMember? EnsureMember(Guid userId, ProjectRole role, DateTimeOffset now)
    {
        if (_members.Any(m => m.UserId == userId)) return null;
        var member = new ProjectMember(Id, userId, role, now);
        _members.Add(member);
        return member;
    }

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
