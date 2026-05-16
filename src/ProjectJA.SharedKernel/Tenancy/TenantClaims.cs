// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Tenancy;

public static class TenantClaims
{
    /// <summary>
    /// Claim type stamped into the auth cookie at sign-in. Carries the tenant Guid the user belongs to.
    /// </summary>
    public const string TenantId = "tenant_id";
}
