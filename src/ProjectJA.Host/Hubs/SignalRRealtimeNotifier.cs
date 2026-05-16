// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using ProjectJA.SharedKernel.Realtime;
using StackExchange.Redis;

namespace ProjectJA.Host.Hubs;

internal sealed class SignalRRealtimeNotifier(
    IHubContext<BoardHub> hub,
    BoardEventStream stream,
    InstanceIdentity instance,
    IConnectionMultiplexer? redis = null) : IRealtimeNotifier
{
    public const string RedisChannelName = "projectja:board";

    public async Task PublishAsync(string groupName, string eventName, object payload, CancellationToken ct)
    {
        // SignalR hub broadcast — reaches non-Blazor clients in this instance, and (if the
        // Redis backplane is wired) other instances' SignalR-connected clients too.
        var hubTask = hub.Clients.Group(groupName).SendAsync(eventName, payload, ct);

        // Local Blazor Server components subscribe to this in-process stream directly.
        var streamTask = stream.PublishAsync(groupName, eventName, payload);

        // Cross-instance fan-out for the BoardEventStream: relay through Redis so that
        // Blazor components on other instances receive the same event.
        var redisTask = Task.CompletedTask;
        if (redis is not null)
        {
            var envelope = new RedisEnvelope(instance.Value, groupName, eventName,
                JsonSerializer.SerializeToElement(payload));
            redisTask = redis.GetSubscriber()
                .PublishAsync(RedisChannel.Literal(RedisChannelName),
                    JsonSerializer.Serialize(envelope));
        }

        await Task.WhenAll(hubTask, streamTask, redisTask).ConfigureAwait(false);
    }

    internal sealed record RedisEnvelope(Guid OriginInstanceId, string GroupName, string EventName, JsonElement Payload);
}
