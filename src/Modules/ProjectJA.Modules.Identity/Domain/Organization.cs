// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Domain;

public sealed class Organization
{
    public Guid Id { get; private set; }
    public string Slug { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Organization() { }

    public Organization(Guid id, string slug, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Slug = slug;
        Name = name;
        CreatedAt = createdAt;
    }

    public static Organization Create(string slug, string name, DateTimeOffset now)
        => new(Guid.NewGuid(), slug, name, now);
}
