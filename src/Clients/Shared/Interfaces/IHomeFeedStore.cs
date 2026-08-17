using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IHomeFeedStore
{
    event Action? Changed;

    bool IsLoading { get; }

    bool IsLoaded { get; }

    bool IsOffline { get; }

    bool CanTrackProgress { get; }

    IReadOnlyList<HomeFeedRow> Rows { get; }

    /// <param name="canTrackProgress">
    /// Caller-resolved <c>CanResumePlayback</c> capability. Must come from the Blazor UI scope
    /// (not a fresh DI scope): WASM auth deserialization is single-consume.
    /// </param>
    /// <param name="identityUserId">
    /// Authenticated identity (NameIdentifier / sub). Reloads when this or the active shared
    /// profile changes so FeedHub keep-alive does not keep the previous user's home.
    /// </param>
    Task EnsureLoadedAsync(
        bool canTrackProgress,
        string? identityUserId,
        CancellationToken cancellationToken = default);

    Task ResetAndReloadAsync(CancellationToken cancellationToken = default);

    void RemoveMedia(string mediaId);

    void RemoveMediaAndChildren(string mediaId);

    void InvalidateCache();

    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-fetches Continue Watching rows from the server (e.g. after returning from playback).
    /// </summary>
    Task RefreshContinueWatchingAsync(CancellationToken cancellationToken = default);
}
