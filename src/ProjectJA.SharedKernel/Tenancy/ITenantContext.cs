// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Tenancy;

public interface ITenantContext
{
    TenantId Current { get; }
    string ConnectionString { get; }
    bool IsResolved { get; }
}
