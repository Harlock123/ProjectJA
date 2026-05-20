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
    /// <summary>Stamped at sign-in from <c>ApplicationUser.IsOrgAdmin</c>. Read by
    /// the <c>OrgAdmin</c> authorization policy. **Sign-out + sign-in required**
    /// for a flag change to take effect — same model as the tenant_id claim.</summary>
    public const string OrgAdminClaimType = "is_org_admin";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (tenant.IsResolved)
            identity.AddClaim(new Claim(TenantClaimType, tenant.Current.Value.ToString()));
        // String "true"/"false" so the OrgAdmin policy can RequireClaim with
        // a literal — Identity's claim store doesn't carry typed values.
        identity.AddClaim(new Claim(OrgAdminClaimType, user.IsOrgAdmin ? "true" : "false"));
        return identity;
    }
}
