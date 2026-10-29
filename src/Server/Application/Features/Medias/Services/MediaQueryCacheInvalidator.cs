using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Medias.Services;

public class MediaQueryCacheInvalidator : IMediaQueryCacheInvalidator
{
    private readonly ConcurrentDictionary<string, long> _userVersions = new(StringComparer.Ordinal);
    private long _version;

    public long Version => Volatile.Read(ref _version);

    public MediaQueryCacheVersion GetVersion(string? identityUserId)
    {
        var userVersion = !string.IsNullOrEmpty(identityUserId)
            && _userVersions.TryGetValue(identityUserId, out var value)
                ? value
                : 0;
        return new MediaQueryCacheVersion(Version, userVersion);
    }

    public void InvalidateAll()
    {
        Interlocked.Increment(ref _version);
    }

    public void InvalidateUser(string identityUserId)
    {
        if (string.IsNullOrEmpty(identityUserId))
        {
            InvalidateAll();
            return;
        }

        _userVersions.AddOrUpdate(identityUserId, 1, static (_, current) => current + 1);
    }
}
