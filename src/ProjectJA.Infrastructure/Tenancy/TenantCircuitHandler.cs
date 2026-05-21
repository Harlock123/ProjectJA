// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProjectJA.Infrastructure.Identity;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class TenantCircuitHandler(
    AuthenticationStateProvider authProvider,
    ITenantDirectory directory,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<TenantCircuitHandler> logger
) : CircuitHandler
{
    private readonly bool _saaSMode = string.Equals(
        configuration["Tenancy:Mode"], "SaaS", StringComparison.OrdinalIgnoreCase);

    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        TenantInfo? info = null;

        var state = await authProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirstValue(TenantStampedClaimsFactory.TenantClaimType);
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var tenantGuid))
        {
            info = await directory.GetByIdAsync(new TenantId(tenantGuid), cancellationToken);
            if (info is null)
                logger.LogWarning("Circuit tenant {Tenant} not present in directory.", tenantGuid);
        }

        // Unauthenticated circuit (e.g. /login): mirror TenantResolutionMiddleware
        // so tenant-bound services resolve before sign-in.
        if (info is null)
        {
            if (_saaSMode)
            {
                var slug = ResolveSlugFromHost(httpContextAccessor.HttpContext?.Request.Host.Host);
                if (slug is not null)
                    info = await directory.FindBySlugAsync(slug, cancellationToken);
            }
            else
            {
                info = await directory.GetDefaultAsync(cancellationToken);
            }
        }

        if (info is null)
        {
            logger.LogDebug("Circuit opened without resolvable tenant; tenant-bound operations will fail.");
            return;
        }

        if (tenantContext is TenantContext tc)
            tc.Set(info);
    }

    private static string? ResolveSlugFromHost(string? host)
    {
        if (string.IsNullOrEmpty(host)) return null;
        var firstDot = host.IndexOf('.');
        if (firstDot <= 0) return null;
        var slug = host[..firstDot];
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }
}
