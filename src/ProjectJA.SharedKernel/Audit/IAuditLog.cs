// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Audit;

public sealed record AuditEntry(
    string Action,
    string ResourceType,
    string ResourceId,
    string Summary,
    object? Detail = null,
    // Explicit actor. Leave null for HTTP callers (the actor is read from the
    // request's auth cookie). Interactive Blazor circuits have no HttpContext,
    // so they must pass the acting user here or the actor would be lost.
    Guid? ActorId = null);

public interface IAuditLog
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct);
}
