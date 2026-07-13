using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos.Notifications;

namespace K7.Clients.Shared.Services;

public sealed class MediaBrowseHubCoordinator : IMediaBrowseHubCoordinator, IDisposable
{
    private readonly K7HubClient _hubClient;
    private readonly List<Subscription> _subscriptions = [];
    private readonly object _sync = new();
    private bool _handlersRegistered;

    public MediaBrowseHubCoordinator(K7HubClient hubClient) => _hubClient = hubClient;

    public IDisposable Subscribe(Guid[]? libraryIds, Guid[]? libraryGroupIds, Action onRefresh)
    {
        RegisterHandlers();

        var subscription = new Subscription(libraryIds, libraryGroupIds, onRefresh);
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

    private void OnBrowseChanged(List<MediaBatchItem> items) => NotifyAll();

    private void OnMediaIndexedFilesUpdated(Guid mediaId, Guid libraryId) =>
        NotifyMatching(libraryId, _ => true);

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount) =>
        NotifyMatching(libraryId, _ => true);

    private void OnMediaMetadataRefreshed(Guid mediaId) =>
        NotifyMatching(Guid.Empty, s => s.OnMetadataOrPictures(mediaId));

    private void OnMediaPicturesUpdated(Guid mediaId) =>
        NotifyMatching(Guid.Empty, s => s.OnMetadataOrPictures(mediaId));

    private void NotifyAll()
    {
        Subscription[] snapshot;
        lock (_sync)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var subscription in snapshot)
            subscription.Notify();
    }

    private void NotifyMatching(Guid libraryId, Func<Subscription, bool> predicate)
    {
        Subscription[] snapshot;
        lock (_sync)
        {
            snapshot = _subscriptions.ToArray();
        }

        foreach (var subscription in snapshot)
        {
            if (libraryId != Guid.Empty
                && !MediaBrowseCarouselRefreshScope.IsAffected(
                    subscription.LibraryIds, subscription.LibraryGroupIds, libraryId))
            {
                continue;
            }

            if (predicate(subscription))
                subscription.Notify();
        }
    }

    private void RemoveSubscription(Subscription subscription)
    {
        lock (_sync)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription(Guid[]? libraryIds, Guid[]? libraryGroupIds, Action onRefresh)
    {
        public Guid[]? LibraryIds { get; } = libraryIds;
        public Guid[]? LibraryGroupIds { get; } = libraryGroupIds;

        public void Notify() => onRefresh();

        public bool OnMetadataOrPictures(Guid mediaId) => true;
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
