namespace K7.Server.Application.Common.Interfaces;

public interface IMediaQueryCacheInvalidator
{
    /// <summary>
    /// Catalog-wide version: bumped by anything that can change what every user sees.
    /// </summary>
    long Version { get; }

    /// <summary>
    /// Combined catalog + per-user version. Use it for caches whose entries are keyed per user,
    /// so a change that only affects one user's state does not wipe everyone else's entries.
    /// </summary>
    MediaQueryCacheVersion GetVersion(string? identityUserId);

    void InvalidateAll();

    /// <summary>
    /// Invalidates the cached media queries of a single user (by ASP.NET Identity id).
    /// </summary>
    void InvalidateUser(string identityUserId);
}

public readonly record struct MediaQueryCacheVersion(long Catalog, long User)
{
    public string ToETag() => $"\"{Catalog}-{User}\"";
}
