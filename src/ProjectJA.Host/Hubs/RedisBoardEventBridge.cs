// SPDX-License-Identifier: BUSL-1.1
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ProjectJA.Host.Hubs;

/// <summary>
/// Subscribes to the Redis pub/sub channel on each app instance and fans incoming
/// messages into the local <see cref="BoardEventStream"/>. Filters out messages
/// originating from this same instance to avoid double-fire echoes.
/// </summary>
internal sealed class RedisBoardEventBridge(
    IConnectionMultiplexer redis,
    BoardEventStream stream,
    InstanceIdentity instance,
    ILogger<RedisBoardEventBridge> logger) : IHostedService
{
    private ChannelMessageQueue? _queue;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = redis.GetSubscriber();
        _queue = await subscriber
            .SubscribeAsync(RedisChannel.Literal(SignalRRealtimeNotifier.RedisChannelName))
            .ConfigureAwait(false);

        _queue.OnMessage(async msg =>
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<SignalRRealtimeNotifier.RedisEnvelope>((string)msg.Message!);
                if (envelope is null) return;
                if (envelope.OriginInstanceId == instance.Value) return; // self-echo
                await stream.PublishAsync(envelope.GroupName, envelope.EventName, envelope.Payload)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to relay Redis board event to local stream");
            }
        });

        logger.LogInformation("Redis board-event bridge subscribed (instance {Instance}).", instance.Value);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_queue is not null)
            await _queue.UnsubscribeAsync().ConfigureAwait(false);
    }
}
