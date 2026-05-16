// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
