using System.Collections.Concurrent;

namespace K7.Clients.Shared.Services;

public sealed class MediaCacheStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly int _maxEntries;

    public MediaCacheStore(int maxEntries = 64)
    {
        _maxEntries = maxEntries;
    }

    public T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.LastAccessed = DateTimeOffset.UtcNow;
            return entry.Data as T;
        }
        return null;
    }

    public void Set<T>(string key, T data) where T : class
    {
        var entry = new CacheEntry { Data = data, LastAccessed = DateTimeOffset.UtcNow };
        _cache[key] = entry;
        EvictIfNeeded();
    }

    public void InvalidateByPrefix(string prefix)
    {
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    public void InvalidateAll()
    {
        _cache.Clear();
    }

    public static string BuildKey(string prefix, params string?[] parts)
    {
        return $"{prefix}:{string.Join(":", parts.Where(p => p is not null))}";
    }

    private void EvictIfNeeded()
    {
        while (_cache.Count > _maxEntries)
        {
            var oldest = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .FirstOrDefault();

            if (oldest.Key is not null)
            {
                _cache.TryRemove(oldest.Key, out _);
            }
        }
    }

    private sealed class CacheEntry
    {
        public required object Data { get; init; }
        public DateTimeOffset LastAccessed { get; set; }
    }
}
