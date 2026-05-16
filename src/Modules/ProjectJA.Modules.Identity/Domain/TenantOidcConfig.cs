// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Domain;

/// <summary>
/// Per-tenant OIDC configuration. Exactly one row per tenant DB (singleton).
/// Used by Host startup to register a tenant-specific OIDC scheme.
/// </summary>
public sealed class TenantOidcConfig
{
    /// <summary>Constant primary key — there's exactly one row per tenant DB.</summary>
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public bool Enabled { get; set; }
    public string DisplayName { get; set; } = "Sign in with SSO";
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool AutoProvision { get; set; }

    /// <summary>JSON-serialized string array of email domains.</summary>
    public string AutoProvisionDomainsJson { get; set; } = "[]";

    public DateTimeOffset UpdatedAt { get; set; }
}
