// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
