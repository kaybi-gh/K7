using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Application.Common.Services;

public sealed class BoundedMemoryCache : MemoryCache, IBoundedMemoryCache
{
    private const int SizeLimit = 5_000;

    public BoundedMemoryCache()
        : base(new MemoryCacheOptions { SizeLimit = SizeLimit })
    {
    }
}
