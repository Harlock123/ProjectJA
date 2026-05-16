// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.DependencyInjection;

namespace ProjectJA.SharedKernel.Tenancy;

public static class TenantScopeExtensions
{
    /// <summary>
    /// Force the scoped <see cref="ITenantContext"/> to a specific tenant. Use only for
    /// out-of-request scopes (bootstrap, background jobs, CLIs) where no middleware ran.
    /// </summary>
    public static void UseTenant(this IServiceProvider scopedServices, TenantInfo info)
        => scopedServices.GetRequiredService<ITenantContextWriter>().Set(info);
}
