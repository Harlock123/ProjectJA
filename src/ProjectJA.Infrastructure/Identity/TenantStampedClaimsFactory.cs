// SPDX-License-Identifier: BUSL-1.1
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ProjectJA.Modules.Identity.Domain;
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Identity;

internal sealed class TenantStampedClaimsFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options,
    ITenantContext tenant)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    public const string TenantClaimType = TenantClaims.TenantId;

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (tenant.IsResolved)
            identity.AddClaim(new Claim(TenantClaimType, tenant.Current.Value.ToString()));
        return identity;
    }
}
