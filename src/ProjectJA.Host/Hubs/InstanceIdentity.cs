// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Hubs;

/// <summary>Singleton id stamped on outbound Redis messages so the originating
/// instance can filter its own echoes when the bridge fans messages back in.</summary>
public sealed class InstanceIdentity
{
    public Guid Value { get; } = Guid.NewGuid();
}
