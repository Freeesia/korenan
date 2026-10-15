using Microsoft.Extensions.Caching.Distributed;

namespace Korenan.ApiService;

/// <summary>
/// ゲーム状態の永続化と変更通知を行う
/// </summary>
public sealed class GameStore(IBufferDistributedCache cache, RoomNotifier notifier)
{
    private static readonly DistributedCacheEntryOptions shortLived = new() { SlidingExpiration = TimeSpan.FromHours(1) };
    private static readonly DistributedCacheEntryOptions imageOptions = new() { SlidingExpiration = TimeSpan.FromDays(1) };

    private static string RoomKey(string roomId) => $"game/room/{roomId}";
    private static string UserRoomKey(Guid userId) => $"user/{userId}/room";
    private static string AikotobaKey(string aikotoba) => $"game/aikotoba/{aikotoba}";
    private static string BotImageKey(string roomId) => $"game/image/bot/{roomId}";

    public ValueTask<Game?> Get(string roomId, CancellationToken token = default)
        => cache.Get<Game>(RoomKey(roomId), token);

    public async ValueTask<string?> GetUserRoom(Guid userId, CancellationToken token = default)
        => await cache.GetStringAsync(UserRoomKey(userId), token) is { Length: > 0 } room ? room : null;

    public async ValueTask<Game?> GetByUser(Guid userId, CancellationToken token = default)
        => await GetUserRoom(userId, token) is { } room ? await Get(room, token) : null;

    public async ValueTask Create(Game game, CancellationToken token = default)
    {
        await cache.Set(RoomKey(game.Id), game, token);
        notifier.Publish(game.Id, game);
    }

    public async ValueTask<Game> Update(string roomId, Func<Game, Game> update, CancellationToken token = default)
    {
        var game = await cache.Update(RoomKey(roomId), update, token);
        notifier.Publish(roomId, game);
        return game;
    }

    public async ValueTask Remove(Game game, CancellationToken token = default)
    {
        foreach (var player in game.Players)
        {
            await RemoveUserRoom(player.Id, token);
        }
        if (await FindRoomByAikotoba(game.Aikotoba, token) == game.Id)
        {
            await ReleaseAikotoba(game.Aikotoba, token);
        }
        await cache.RemoveAsync(RoomKey(game.Id), token);
        await cache.RemoveAsync(BotImageKey(game.Id), token);
        notifier.Publish(game.Id, null);
    }

    public async ValueTask<string?> FindRoomByAikotoba(string aikotoba, CancellationToken token = default)
        => await cache.GetStringAsync(AikotobaKey(aikotoba), token) is { Length: > 0 } room ? room : null;

    public async ValueTask ReserveAikotoba(string aikotoba, string roomId, CancellationToken token = default)
        => await cache.SetStringAsync(AikotobaKey(aikotoba), roomId, shortLived, token);

    public async ValueTask ReleaseAikotoba(string aikotoba, CancellationToken token = default)
        => await cache.RemoveAsync(AikotobaKey(aikotoba), token);

    public async ValueTask SetUserRoom(Guid userId, string roomId, CancellationToken token = default)
        => await cache.SetStringAsync(UserRoomKey(userId), roomId, shortLived, token);

    public async ValueTask RemoveUserRoom(Guid userId, CancellationToken token = default)
        => await cache.RemoveAsync(UserRoomKey(userId), token);

    public async ValueTask SetBotImage(string roomId, byte[] image, CancellationToken token = default)
        => await cache.SetAsync(BotImageKey(roomId), image, imageOptions, token);

    public async ValueTask<byte[]?> GetBotImage(string roomId, CancellationToken token = default)
        => await cache.GetAsync(BotImageKey(roomId), token);
}
