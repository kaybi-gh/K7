namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Serializes media creation per media identity.
/// </summary>
/// <remarks>
/// Creating a media is a check-then-insert: look the media up by external id, then by title, then create
/// it. Nothing in the database prevents a duplicate - Medias has no unique constraint and ExternalIds is
/// indexed but not unique - so two tasks running that sequence concurrently for the same album, serie or
/// movie both miss the lookup and both insert. K7 has no merge tooling, so a duplicate media is a manual
/// cleanup. This lock provides the missing mutual exclusion.
/// <para>
/// Held in-process, consistent with the concurrency gate and the cancellation registry: K7 runs as a
/// single instance against a given database (see docs/admin/operating.md).
/// </para>
/// </remarks>
public interface IMediaIdentityLock
{
    /// <summary>
    /// Waits until no other media creation is in flight for <paramref name="identityKey"/>.
    /// </summary>
    /// <param name="identityKey">Stable key of the media identity, built by <c>MediaIdentityKey</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A handle releasing the lock when disposed.</returns>
    Task<IAsyncDisposable> AcquireAsync(string identityKey, CancellationToken cancellationToken = default);
}
