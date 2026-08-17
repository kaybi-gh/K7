using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Session-scoped overlay of user ratings. Fed by local RatingStars edits and SignalR.
/// </summary>
public sealed class UserRatingSync : IUserRatingSync, IDisposable
{
    private readonly K7HubClient? _hub;
    private readonly Dictionary<Guid, int?> _ratings = [];
    private readonly object _lock = new();

    public UserRatingSync(IEnumerable<K7HubClient> hubs)
    {
        _hub = hubs.FirstOrDefault();
        if (_hub is null)
            return;

        _hub.UserRatingUpdated += OnHubRatingUpdated;
        _hub.UserContextChanged += OnUserContextChanged;
    }

    public event Action<Guid, int?>? Changed;

    public bool TryGet(Guid mediaId, out int? value)
    {
        lock (_lock)
            return _ratings.TryGetValue(mediaId, out value);
    }

    public void Set(Guid mediaId, int? value)
    {
        var normalized = value is > 0 ? value : null;
        lock (_lock)
        {
            if (_ratings.TryGetValue(mediaId, out var existing) && existing == normalized)
                return;
            _ratings[mediaId] = normalized;
        }

        Changed?.Invoke(mediaId, normalized);
    }

    public void Clear()
    {
        lock (_lock)
            _ratings.Clear();
    }

    public void Dispose()
    {
        if (_hub is null)
            return;

        _hub.UserRatingUpdated -= OnHubRatingUpdated;
        _hub.UserContextChanged -= OnUserContextChanged;
    }

    private void OnHubRatingUpdated(Guid mediaId, int value) =>
        Set(mediaId, value > 0 ? value : null);

    private void OnUserContextChanged() => Clear();
}
