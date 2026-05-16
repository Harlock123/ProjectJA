// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Audit.Domain;

public sealed class AuditEvent
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? ActorId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string Action { get; init; } = default!;
    public string ResourceType { get; init; } = default!;
    public string ResourceId { get; init; } = default!;
    public string Summary { get; init; } = default!;
    public string? Detail { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
