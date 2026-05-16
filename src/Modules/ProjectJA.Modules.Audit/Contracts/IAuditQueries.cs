// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Audit.Contracts;

public sealed record AuditEntryView(
    Guid Id,
    Guid? ActorId,
    DateTimeOffset OccurredAt,
    string Action,
    string ResourceType,
    string ResourceId,
    string Summary,
    string? IpAddress);

public interface IAuditQueries
{
    Task<IReadOnlyList<AuditEntryView>> ListRecentAsync(int limit, CancellationToken ct);
}
