using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Medias.Services;

public class MediaQueryCacheInvalidator : IMediaQueryCacheInvalidator
{
    private long _version;

    public long Version => Volatile.Read(ref _version);

    public void InvalidateAll()
    {
        Interlocked.Increment(ref _version);
    }
}
