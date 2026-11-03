using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Medias.Services;

public class MediaQueryCacheInvalidator : IMediaQueryCacheInvalidator
{
    private readonly string _epoch = Guid.NewGuid().ToString("N")[..8];
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

    public string GetETag(string? identityUserId, Guid? sharedProfileId)
    {
        var version = GetVersion(identityUserId);
        // string hashes are randomized per process, which the epoch already scopes.
        var scope = HashCode.Combine(identityUserId ?? string.Empty, sharedProfileId);
        return $"\"{_epoch}-{scope:x8}-{version.Catalog}-{version.User}\"";
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
