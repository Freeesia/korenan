using System.Collections.Concurrent;

namespace Korenan.ApiService;

public class KeyedAsyncLock
{
    private readonly object locker = new();
    private readonly ConcurrentDictionary<string, Lazy<SemaphoreWithRefs>> locks = new();
    private readonly ConcurrentStack<SemaphoreWithRefs> pool = new();

    public async ValueTask<IDisposable> LockAsync(string key, CancellationToken token = default)
    {
        var semaphore = default(SemaphoreSlim);
        lock (locker)
        {
            var pair = locks.GetOrAdd(key, _ => GetLazySemaphore()).Value;
            pair.Refs++;
            semaphore = pair.Semaphore;
        }
        await semaphore.WaitAsync(token).ConfigureAwait(false);
        return new LockReleaser(key, semaphore, this);
    }

    public bool IsLocked(string key)
    {
        if (!locks.TryGetValue(key, out var lazySemaphore))
        {
            return false;
        }
        return lazySemaphore.Value.Semaphore.CurrentCount == 0;
    }

    private Lazy<SemaphoreWithRefs> GetLazySemaphore() => new(() => pool.TryPop(out var s) ? s : new(new(1, 1)));

    private void ReleaseAndRemove(string key, SemaphoreSlim semaphore)
    {
        semaphore.Release();
        lock (locker)
        {
            var pair = locks[key].Value;
            pair.Refs--;
            if (pair.Refs == 0)
            {
                locks.TryRemove(key, out _);
                pool.Push(pair);
            }
        }
    }

    private readonly struct LockReleaser(string key, SemaphoreSlim semaphore, KeyedAsyncLock keyedAsyncLock) : IDisposable
    {
        private readonly string key = key;
        private readonly SemaphoreSlim semaphore = semaphore;
        private readonly KeyedAsyncLock keyedAsyncLock = keyedAsyncLock;

        public void Dispose() => keyedAsyncLock.ReleaseAndRemove(key, semaphore);
    }

    private record SemaphoreWithRefs(SemaphoreSlim Semaphore)
    {
        public int Refs { get; set; }
    }
}