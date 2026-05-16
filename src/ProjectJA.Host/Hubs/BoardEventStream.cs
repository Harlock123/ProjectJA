// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.Host.Hubs;

/// <summary>
/// In-process pub/sub for Blazor Server components. Components subscribe per group;
/// the SignalRRealtimeNotifier publishes here in addition to the SignalR hub so
/// server-side Blazor pages can react without round-tripping through a HubConnection.
/// </summary>
public sealed class BoardEventStream
{
    private readonly object _lock = new();
    private readonly Dictionary<string, List<Func<string, object, Task>>> _subscribers = new();

    public IDisposable Subscribe(string groupName, Func<string, object, Task> handler)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(groupName, out var list))
                _subscribers[groupName] = list = new List<Func<string, object, Task>>();
            list.Add(handler);
        }
        return new Subscription(this, groupName, handler);
    }

    public async Task PublishAsync(string groupName, string eventName, object payload)
    {
        Func<string, object, Task>[] handlers;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(groupName, out var list) || list.Count == 0)
                return;
            handlers = list.ToArray();
        }
        foreach (var handler in handlers)
            await handler(eventName, payload).ConfigureAwait(false);
    }

    private void Remove(string groupName, Func<string, object, Task> handler)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(groupName, out var list)) return;
            list.Remove(handler);
            if (list.Count == 0) _subscribers.Remove(groupName);
        }
    }

    private sealed class Subscription(BoardEventStream stream, string group, Func<string, object, Task> handler)
        : IDisposable
    {
        public void Dispose() => stream.Remove(group, handler);
    }
}
