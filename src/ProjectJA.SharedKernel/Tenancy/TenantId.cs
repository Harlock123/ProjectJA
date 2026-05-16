// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Tenancy;

public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
