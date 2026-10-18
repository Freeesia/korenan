using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;

namespace Korenan.ApiService;

/// <summary>
/// ルーム単位でゲーム状態の変更を購読者へ通知する
/// </summary>
public sealed class RoomNotifier
{
    private static readonly BoundedChannelOptions channelOptions = new(1)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
    };

    private readonly ConcurrentDictionary<string, ImmutableList<Channel<Game?>>> subscribers = new();
    private readonly ConcurrentDictionary<string, byte> activeRooms = new();

    /// <summary>
    /// 進行中のルームID一覧
    /// </summary>
    public IReadOnlyCollection<string> ActiveRooms => [.. activeRooms.Keys];

    /// <summary>
    /// ルームの状態変更を通知する
    /// </summary>
    /// <param name="roomId">ルームID</param>
    /// <param name="game">最新のゲーム状態。ルームが削除された場合はnull</param>
    public void Publish(string roomId, Game? game)
    {
        if (game is null)
        {
            activeRooms.TryRemove(roomId, out _);
        }
        else
        {
            activeRooms.TryAdd(roomId, 0);
        }
        if (!subscribers.TryGetValue(roomId, out var channels))
        {
            return;
        }
        foreach (var channel in channels)
        {
            channel.Writer.TryWrite(game);
        }
    }

    /// <summary>
    /// ルームを進行中一覧から外す
    /// </summary>
    public void Deactivate(string roomId) => activeRooms.TryRemove(roomId, out _);

    /// <summary>
    /// ルームの状態変更を購読する
    /// </summary>
    public Subscription Subscribe(string roomId)
    {
        var channel = Channel.CreateBounded<Game?>(channelOptions);
        activeRooms.TryAdd(roomId, 0);
        subscribers.AddOrUpdate(roomId, _ => [channel], (_, list) => list.Add(channel));
        return new(this, roomId, channel);
    }

    private void Unsubscribe(string roomId, Channel<Game?> channel)
    {
        channel.Writer.TryComplete();
        var list = subscribers.AddOrUpdate(roomId, _ => [], (_, list) => list.Remove(channel));
        if (list.IsEmpty)
        {
            subscribers.TryRemove(new(roomId, list));
        }
    }

    public sealed class Subscription(RoomNotifier notifier, string roomId, Channel<Game?> channel) : IDisposable
    {
        public ChannelReader<Game?> Reader => channel.Reader;

        public void Dispose() => notifier.Unsubscribe(roomId, channel);
    }
}
