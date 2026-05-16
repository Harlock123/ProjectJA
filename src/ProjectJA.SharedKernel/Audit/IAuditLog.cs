// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Audit;

public sealed record AuditEntry(
    string Action,
    string ResourceType,
    string ResourceId,
    string Summary,
    object? Detail = null);

public interface IAuditLog
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct);
}
