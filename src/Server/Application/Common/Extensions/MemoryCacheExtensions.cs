using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Application.Common.Extensions;

public static class MemoryCacheExtensions
{
    public const int EntrySize = 1;

    public static void SetWithSize<T>(this IMemoryCache cache, object key, T value) =>
        cache.Set(key, value, new MemoryCacheEntryOptions { Size = EntrySize });

    public static void SetWithSize<T>(this IMemoryCache cache, object key, T value, TimeSpan absoluteExpirationRelativeToNow) =>
        cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpirationRelativeToNow,
            Size = EntrySize
        });

    public static void SetWithSize<T>(this IMemoryCache cache, object key, T value, MemoryCacheEntryOptions options)
    {
        options.Size = EntrySize;
        cache.Set(key, value, options);
    }
}
