using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IHomeFeedStore
{
    event Action? Changed;

    bool IsLoading { get; }

    bool IsOffline { get; }

    bool CanTrackProgress { get; }

    IReadOnlyList<HomeFeedRow> Rows { get; }

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task ResetAndReloadAsync(CancellationToken cancellationToken = default);

    void RemoveMedia(string mediaId);

    void RemoveMediaAndChildren(string mediaId);

    void InvalidateCache();

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
