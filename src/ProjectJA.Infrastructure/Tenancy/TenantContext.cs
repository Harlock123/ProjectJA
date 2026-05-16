// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.SharedKernel.Tenancy;

namespace ProjectJA.Infrastructure.Tenancy;

internal sealed class TenantContext : ITenantContext, ITenantContextWriter
{
    private TenantInfo? _resolved;

    public TenantId Current => _resolved?.Id
        ?? throw new InvalidOperationException("Tenant has not been resolved for this scope.");

    public string ConnectionString => _resolved?.ConnectionString
        ?? throw new InvalidOperationException("Tenant has not been resolved for this scope.");

    public bool IsResolved => _resolved is not null;

    public void Set(TenantInfo info) => _resolved = info;
}
