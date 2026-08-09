using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Notifications;

namespace K7.Clients.Shared.Services;

public sealed class MediaBrowseHubCoordinator : IMediaBrowseHubCoordinator, IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly List<Subscription> _subscriptions = [];
    private readonly object _sync = new();
    private bool _handlersRegistered;

    public MediaBrowseHubCoordinator(K7HubClient hubClient) => _hubClient = hubClient;

    public IDisposable Subscribe(
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        Action onCatalogChanged,
        Action<Guid>? onMediaVisualChanged = null,
        IReadOnlyCollection<MediaType>? mediaTypes = null)
    {
        RegisterHandlers();

        var subscription = new Subscription(libraryIds, libraryGroupIds, mediaTypes, onCatalogChanged, onMediaVisualChanged);
        lock (_sync)
        {
            _subscriptions.Add(subscription);
        }

        return new SubscriptionHandle(this, subscription);
    }

    public void Dispose() => UnregisterHandlers();

    private void RegisterHandlers()
    {
        if (_handlersRegistered)
            return;

        _handlersRegistered = true;
        _hubClient.MediaBatchAdded += OnBrowseChanged;
        _hubClient.MediaIndexedFilesUpdated += OnMediaIndexedFilesUpdated;
        _hubClient.LibraryScanCompleted += OnLibraryScanCompleted;
        _hubClient.MediaMetadataRefreshed += OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated += OnMediaPicturesUpdated;
    }

    private void UnregisterHandlers()
    {
        if (!_handlersRegistered)
            return;

        _handlersRegistered = false;
        _hubClient.MediaBatchAdded -= OnBrowseChanged;
        _hubClient.MediaIndexedFilesUpdated -= OnMediaIndexedFilesUpdated;
        _hubClient.LibraryScanCompleted -= OnLibraryScanCompleted;
        _hubClient.MediaMetadataRefreshed -= OnMediaMetadataRefreshed;
        _hubClient.MediaPicturesUpdated -= OnMediaPicturesUpdated;
    }

    private void OnBrowseChanged(List<MediaBatchItem> items) => NotifyCatalogMatchingBatch(items);

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId) =>
        NotifyCatalogMatchingLibrary(libraryId);

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount) =>
        NotifyCatalogMatchingLibrary(libraryId);

    private void OnMediaMetadataRefreshed(Guid mediaId) => NotifyMediaVisual(mediaId);

    private void OnMediaPicturesUpdated(Guid mediaId) => NotifyMediaVisual(mediaId);

    private void NotifyCatalogMatchingBatch(List<MediaBatchItem> items)
    {
        Subscription[] snapshot;
        lock (_sync)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var subscription in snapshot)
        {
            if (!MediaBrowseCarouselRefreshScope.IsBatchAffected(
                    subscription.LibraryIds,
                    subscription.LibraryGroupIds,
                    subscription.MediaTypes,
                    items))
            {
                continue;
            }

            subscription.NotifyCatalog();
        }
    }

    private void NotifyCatalogMatchingLibrary(Guid libraryId)
    {
        Subscription[] snapshot;
        lock (_sync)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var subscription in snapshot)
        {
            if (!MediaBrowseCarouselRefreshScope.IsAffected(
                    subscription.LibraryIds, subscription.LibraryGroupIds, libraryId))
            {
                continue;
            }

            subscription.NotifyCatalog();
        }
    }

    private void NotifyMediaVisual(Guid mediaId)
    {
        Subscription[] snapshot;
        lock (_sync)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var subscription in snapshot)
            subscription.NotifyMediaVisual(mediaId);
    }

    private void RemoveSubscription(Subscription subscription)
    {
        lock (_sync)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription(
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        IReadOnlyCollection<MediaType>? mediaTypes,
        Action onCatalogChanged,
        Action<Guid>? onMediaVisualChanged)
    {
        public Guid[]? LibraryIds { get; } = libraryIds;
        public Guid[]? LibraryGroupIds { get; } = libraryGroupIds;
        public IReadOnlyCollection<MediaType>? MediaTypes { get; } = mediaTypes;

        public void NotifyCatalog() => onCatalogChanged();

        public void NotifyMediaVisual(Guid mediaId)
        {
            if (onMediaVisualChanged is not null)
                onMediaVisualChanged(mediaId);
            else
                onCatalogChanged();
        }
    }

    private sealed class SubscriptionHandle(MediaBrowseHubCoordinator coordinator, Subscription subscription) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            coordinator.RemoveSubscription(subscription);
        }
    }
}
