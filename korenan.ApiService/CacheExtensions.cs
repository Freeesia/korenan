using CommunityToolkit.HighPerformance.Buffers;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Caching.Distributed;

namespace Korenan.ApiService;

public static class CacheExtensions
{
    private static readonly MessagePackSerializerOptions serializeOptions = MessagePackSerializerOptions.Standard.WithResolver(StandardResolver.Instance);
    private static readonly DistributedCacheEntryOptions defaultOptions = new() { SlidingExpiration = TimeSpan.FromHours(1) };

    public static async ValueTask Set<T>(this IBufferDistributedCache cache, string key, T data, CancellationToken token = default)
    {
        using var buffer = new ArrayPoolBufferWriter<byte>();
        MessagePackSerializer.Serialize(buffer, data, serializeOptions, token);
        await cache.SetAsync(key, new(buffer.WrittenMemory), defaultOptions, token);
    }

    public static async ValueTask Update<T>(this IBufferDistributedCache cache, string key, Func<T, T> update, CancellationToken token = default)
    {
        var data = await cache.Get<T>(key, token) ?? throw new InvalidOperationException("Cache data not found");
        data = update(data);
        using var buffer = new ArrayPoolBufferWriter<byte>();
        MessagePackSerializer.Serialize(buffer, data, serializeOptions, token);
        await cache.SetAsync(key, new(buffer.WrittenMemory), defaultOptions, token);
    }

    public static async ValueTask Update<T>(this IBufferDistributedCache cache, string key, Action<T> update, CancellationToken token = default)
    {
        var data = await cache.Get<T>(key, token) ?? throw new InvalidOperationException("Cache data not found");
        update(data);
        using var buffer = new ArrayPoolBufferWriter<byte>();
        MessagePackSerializer.Serialize(buffer, data, serializeOptions, token);
        await cache.SetAsync(key, new(buffer.WrittenMemory), defaultOptions, token);
    }

    public static async ValueTask<T?> Get<T>(this IBufferDistributedCache cache, string key, CancellationToken token = default)
    {
        using var buffer = new ArrayPoolBufferWriter<byte>();
        if (!await cache.TryGetAsync(key, buffer, token))
        {
            return default;
        }
        return MessagePackSerializer.Deserialize<T>(buffer.WrittenMemory, serializeOptions, token);
    }
}
