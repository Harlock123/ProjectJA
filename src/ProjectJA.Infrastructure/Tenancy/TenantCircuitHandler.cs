// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProjectJA.Infrastructure.Identity;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class TenantCircuitHandler(
    AuthenticationStateProvider authProvider,
    ITenantDirectory directory,
    ITenantContext tenantContext,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TenantCircuitHandler> logger
) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await authProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirstValue(TenantStampedClaimsFactory.TenantClaimType);

        TenantInfo? info;
        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var tenantGuid))
        {
            info = await directory.GetByIdAsync(new TenantId(tenantGuid), cancellationToken);
            if (info is null)
            {
                logger.LogWarning("Circuit tenant {Tenant} not present in directory.", tenantGuid);
                return;
            }
        }
        else
        {
            // Unauthenticated circuit (e.g. the login page): resolve from the slug that
            // TenantResolutionMiddleware already placed in HttpContext.Items, or fall back
            // to the default tenant. Without this, DB-backed pages like /login crash
            // because AppDbContext cannot build its connection string.
            var slug = httpContextAccessor.HttpContext?.Items["Tenant.Slug"] as string;
            info = !string.IsNullOrEmpty(slug)
                ? await directory.FindBySlugAsync(slug, cancellationToken)
                : await directory.GetDefaultAsync(cancellationToken);

            if (info is null)
            {
                logger.LogWarning("Could not resolve tenant for unauthenticated circuit; tenant-bound operations will fail.");
                return;
            }
        }

        if (tenantContext is TenantContext tc)
            tc.Set(info);
    }
}
