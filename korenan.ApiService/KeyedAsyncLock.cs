using System.Collections.Concurrent;

namespace Korenan.ApiService;

public class KeyedAsyncLock
{
    private readonly ConcurrentDictionary<string, Lazy<SemaphoreSlim>> locks = new();

    public async ValueTask<IDisposable> LockAsync(string key, CancellationToken token = default)
    {
        var semaphore = locks.GetOrAdd(key, _ => GetLazySemaphore()).Value;
        await semaphore.WaitAsync(token).ConfigureAwait(false);
        return new LockReleaser(semaphore);
    }

    public bool IsLocked(string key)
    {
        if (!locks.TryGetValue(key, out var lazySemaphore))
        {
            return false;
        }
        return lazySemaphore.Value.CurrentCount == 0;
    }

    private static Lazy<SemaphoreSlim> GetLazySemaphore() => new(() => new SemaphoreSlim(1, 1), true);

    private readonly struct LockReleaser(SemaphoreSlim semaphore) : IDisposable
    {
        private readonly SemaphoreSlim semaphore = semaphore;

        public void Dispose() => semaphore.Release();
    }
}