// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Tenancy;

/// <summary>
/// Mutator side of the tenant context. Resolved internally by
/// <see cref="TenantScopeExtensions.UseTenant"/> for out-of-request scopes
/// (background jobs, CLIs, bootstrap). Day-to-day callers should depend on
/// <see cref="ITenantContext"/> instead.
/// </summary>
public interface ITenantContextWriter
{
    void Set(TenantInfo info);
}
