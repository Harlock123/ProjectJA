// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Realtime;

/// <summary>
/// Broadcasts a server-side event to all clients subscribed to a group.
/// Modules depend on this interface, not on SignalR — the implementation lives in
/// the composition root (Host).
/// </summary>
public interface IRealtimeNotifier
{
    Task PublishAsync(string groupName, string eventName, object payload, CancellationToken ct);
}
