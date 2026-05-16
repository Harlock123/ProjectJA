// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProjectJA.Modules.Identity.Contracts;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Audit;
using ProjectJA.SharedKernel.Time;

namespace ProjectJA.Modules.Identity.Application;

internal sealed class OidcConfigService(DbContext db, IClock clock, IAuditLog audit) : IOidcConfigService
{
    public async Task<OidcConfigView?> GetAsync(CancellationToken ct)
    {
        var entity = await db.Set<TenantOidcConfig>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == TenantOidcConfig.SingletonId, ct);
        if (entity is null) return null;
        return new OidcConfigView(
            entity.Enabled,
            entity.DisplayName,
            entity.Authority,
            entity.ClientId,
            !string.IsNullOrEmpty(entity.ClientSecret),
            entity.AutoProvision,
            ParseDomains(entity.AutoProvisionDomainsJson),
            entity.UpdatedAt);
    }

    public async Task<OidcConfigSecret?> GetForSchemeRegistrationAsync(CancellationToken ct)
    {
        var entity = await db.Set<TenantOidcConfig>().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == TenantOidcConfig.SingletonId, ct);
        if (entity is null) return null;
        return new OidcConfigSecret(
            entity.Enabled,
            entity.DisplayName,
            entity.Authority,
            entity.ClientId,
            entity.ClientSecret,
            entity.AutoProvision,
            ParseDomains(entity.AutoProvisionDomainsJson));
    }

    public async Task SaveAsync(SaveOidcConfigRequest req, CancellationToken ct)
    {
        var entity = await db.Set<TenantOidcConfig>()
            .FirstOrDefaultAsync(c => c.Id == TenantOidcConfig.SingletonId, ct);

        var isNew = entity is null;
        entity ??= new TenantOidcConfig { Id = TenantOidcConfig.SingletonId };

        entity.Enabled = req.Enabled;
        entity.DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? "Sign in with SSO" : req.DisplayName.Trim();
        entity.Authority = req.Authority?.Trim();
        entity.ClientId = req.ClientId?.Trim();
        if (req.ClientSecret is not null && req.ClientSecret.Length > 0)
            entity.ClientSecret = req.ClientSecret; // overwrite only when caller supplied a new value
        entity.AutoProvision = req.AutoProvision;
        entity.AutoProvisionDomainsJson = JsonSerializer.Serialize(req.AutoProvisionDomains ?? Array.Empty<string>());
        entity.UpdatedAt = clock.UtcNow;

        if (isNew) db.Set<TenantOidcConfig>().Add(entity);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "oidc-config.saved",
            ResourceType: "TenantOidcConfig",
            ResourceId: TenantOidcConfig.SingletonId.ToString(),
            Summary: req.Enabled ? "OIDC configuration enabled/updated" : "OIDC configuration disabled",
            Detail: new { req.Enabled, authoritySet = !string.IsNullOrWhiteSpace(req.Authority) }), ct);
    }

    public async Task<bool> ClearAsync(CancellationToken ct)
    {
        var entity = await db.Set<TenantOidcConfig>()
            .FirstOrDefaultAsync(c => c.Id == TenantOidcConfig.SingletonId, ct);
        if (entity is null) return false;
        db.Set<TenantOidcConfig>().Remove(entity);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(new AuditEntry(
            Action: "oidc-config.cleared",
            ResourceType: "TenantOidcConfig",
            ResourceId: TenantOidcConfig.SingletonId.ToString(),
            Summary: "OIDC configuration cleared"), ct);
        return true;
    }

    private static IReadOnlyList<string> ParseDomains(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
