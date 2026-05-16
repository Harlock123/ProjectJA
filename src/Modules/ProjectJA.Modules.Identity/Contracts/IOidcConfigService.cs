// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Modules.Identity.Contracts;

public sealed record OidcConfigView(
    bool Enabled,
    string DisplayName,
    string? Authority,
    string? ClientId,
    bool HasClientSecret,
    bool AutoProvision,
    IReadOnlyList<string> AutoProvisionDomains,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Full config including the client secret. Returned only to the in-process Host
/// during scheme registration at startup. NEVER expose over the network.
/// </summary>
public sealed record OidcConfigSecret(
    bool Enabled,
    string DisplayName,
    string? Authority,
    string? ClientId,
    string? ClientSecret,
    bool AutoProvision,
    IReadOnlyList<string> AutoProvisionDomains);

public sealed record SaveOidcConfigRequest(
    bool Enabled,
    string DisplayName,
    string? Authority,
    string? ClientId,
    string? ClientSecret,
    bool AutoProvision,
    IReadOnlyList<string> AutoProvisionDomains);

public interface IOidcConfigService
{
    Task<OidcConfigView?> GetAsync(CancellationToken ct);
    Task<OidcConfigSecret?> GetForSchemeRegistrationAsync(CancellationToken ct);
    Task SaveAsync(SaveOidcConfigRequest req, CancellationToken ct);
    Task<bool> ClearAsync(CancellationToken ct);
}
