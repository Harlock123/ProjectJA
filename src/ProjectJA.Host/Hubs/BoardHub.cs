// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Host.Hubs;

[Authorize]
public sealed class BoardHub : Hub
{
    public static string GroupName(Guid tenantId, Guid projectId) =>
        $"tenant:{tenantId:N}:project:{projectId:N}";

    public async Task JoinProject(Guid projectId)
    {
        var tenantClaim = Context.User?.FindFirstValue(TenantClaims.TenantId);
        if (!Guid.TryParse(tenantClaim, out var tenantId))
            throw new HubException("Connection has no tenant claim.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tenantId, projectId));
    }

    public async Task LeaveProject(Guid projectId)
    {
        var tenantClaim = Context.User?.FindFirstValue(TenantClaims.TenantId);
        if (!Guid.TryParse(tenantClaim, out var tenantId)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tenantId, projectId));
    }
}
