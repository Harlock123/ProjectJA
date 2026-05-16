// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Domain;

public sealed class Invite
{
    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Email { get; private set; } = default!;
    public string TokenHash { get; private set; } = default!;
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }

    private Invite() { }

    public Invite(
        Guid id,
        Guid organizationId,
        string email,
        string tokenHash,
        Guid createdById,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Email = email;
        TokenHash = tokenHash;
        CreatedById = createdById;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public static Invite Create(
        Guid organizationId,
        string email,
        string tokenHash,
        Guid createdById,
        DateTimeOffset now,
        TimeSpan ttl)
        => new(Guid.NewGuid(), organizationId, email.Trim().ToLowerInvariant(),
               tokenHash, createdById, now, now.Add(ttl));

    public bool IsActive(DateTimeOffset now) => AcceptedAt is null && now < ExpiresAt;

    public void MarkAccepted(DateTimeOffset now) => AcceptedAt = now;
}
