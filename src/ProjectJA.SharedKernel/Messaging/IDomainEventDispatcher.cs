// SPDX-License-Identifier: BUSL-1.1
using ProjectJA.SharedKernel.Domain;

namespace ProjectJA.SharedKernel.Messaging;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken ct);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent @event, CancellationToken ct);
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}
