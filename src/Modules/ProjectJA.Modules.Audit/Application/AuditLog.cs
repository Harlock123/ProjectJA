// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Audit.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Tenancy;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Audit.Application;

internal sealed class AuditLog(
    DbContext db,
    ITenantContext tenant,
    IHttpContextAccessor http,
    IClock clock) : IAuditLog
{
    public async Task RecordAsync(AuditEntry entry, CancellationToken ct)
    {
        var ctx = http.HttpContext;
        // An explicit actor (interactive Blazor callers) wins; otherwise read it
        // from the current request's auth cookie (HTTP endpoint callers).
        Guid? actorId = entry.ActorId;
        if (actorId is null && ctx?.User is { Identity.IsAuthenticated: true })
        {
            var claim = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(claim, out var parsed)) actorId = parsed;
        }

        var ev = new AuditEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Current.Value,
            ActorId = actorId,
            OccurredAt = clock.UtcNow,
            Action = entry.Action,
            ResourceType = entry.ResourceType,
            ResourceId = entry.ResourceId,
            Summary = entry.Summary,
            Detail = entry.Detail is null ? null : JsonSerializer.Serialize(entry.Detail),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx?.Request.Headers.UserAgent.ToString(),
        };

        db.Set<AuditEvent>().Add(ev);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
