// SPDX-License-Identifier: BUSL-1.1
using Microsoft.Extensions.DependencyInjection;
using ProjectJA.SharedKernel.Domain;
using ProjectJA.SharedKernel.Messaging;

namespace ProjectJA.Infrastructure.Messaging;

public sealed class InProcessDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _services;

    public InProcessDomainEventDispatcher(IServiceProvider services) => _services = services;

    public async Task DispatchAsync(IDomainEvent @event, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(@event.GetType());
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerType);
        var handlers = (IEnumerable<object>)_services.GetRequiredService(enumerableType);
        var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
        foreach (var handler in handlers)
        {
            var task = (Task)method.Invoke(handler, new object[] { @event, ct })!;
            await task.ConfigureAwait(false);
        }
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var @event in events)
            await DispatchAsync(@event, ct).ConfigureAwait(false);
    }
}
