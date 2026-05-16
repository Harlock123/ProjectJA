// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.Logging;
using ProjectJA.Infrastructure.Identity;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class TenantCircuitHandler(
    AuthenticationStateProvider authProvider,
    ITenantDirectory directory,
    ITenantContext tenantContext,
    ILogger<TenantCircuitHandler> logger
) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var state = await authProvider.GetAuthenticationStateAsync();
        var claim = state.User.FindFirstValue(TenantStampedClaimsFactory.TenantClaimType);

        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var tenantGuid))
        {
            logger.LogDebug("Circuit opened without tenant claim; tenant-bound operations will fail.");
            return;
        }

        var info = await directory.GetByIdAsync(new TenantId(tenantGuid), cancellationToken);
        if (info is null)
        {
            logger.LogWarning("Circuit tenant {Tenant} not present in directory.", tenantGuid);
            return;
        }

        if (tenantContext is TenantContext tc)
            tc.Set(info);
    }
}
