using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class FeedHubHostService : IFeedHubHostService
{
    private readonly List<FeedHubKey> _mountedKeys = [];
    private readonly object _sync = new();
    private int? _maxNonHomeKeys;

    public event Action? Changed;

    public bool IsEnabled { get; private set; }

    public bool IsHubRouteActive { get; private set; }

    public FeedHubKey? ActiveKey { get; private set; }

    public IReadOnlyList<FeedHubKey> MountedKeys
    {
        get
        {
            lock (_sync)
            {
                return _mountedKeys.ToList();
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
            return;

        IsEnabled = enabled;
        if (!enabled)
        {
            lock (_sync)
            {
                _mountedKeys.Clear();
            }

            ActiveKey = null;
            IsHubRouteActive = false;
        }

        NotifyChanged();
    }

    public void SetMountLimit(int? maxNonHomeKeys)
    {
        if (maxNonHomeKeys is < 0)
            throw new ArgumentOutOfRangeException(nameof(maxNonHomeKeys));

        if (_maxNonHomeKeys == maxNonHomeKeys)
            return;

        _maxNonHomeKeys = maxNonHomeKeys;

        lock (_sync)
        {
            if (PruneNonHomeKeysUnlocked())
                NotifyChanged();
        }
    }

    public bool ShouldDelegateRoute(string uri) => IsEnabled && TryParseHubRoute(uri, out _);

    private bool TryParseHubRoute(string uri, out FeedHubKey key)
    {
        key = default;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return false;

        var path = absolute.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        if (path == "/")
        {
            key = FeedHubKey.Home;
            return true;
        }

        const string libraryPrefix = "/library-groups/";
        if (path.StartsWith(libraryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var idSegment = path[libraryPrefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (Guid.TryParse(idSegment, out var groupId))
            {
                key = FeedHubKey.ForLibraryGroup(groupId);
                return true;
            }
        }

        if (!path.Equals("/explore", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = absolute.Query;
        if (TryGetQueryValue(query, "library-group", out var groupValue)
            && Guid.TryParse(groupValue, out var exploreGroupId))
        {
            key = FeedHubKey.ForExploreGroup(exploreGroupId);
            return true;
        }

        return false;
    }

    public void UpdateLocation(string uri)
    {
        if (!IsEnabled)
            return;

        if (TryParseHubRoute(uri, out var key))
        {
            lock (_sync)
            {
                TouchMountedKeyUnlocked(key);
                PruneNonHomeKeysUnlocked();
            }

            ActiveKey = key;
            IsHubRouteActive = true;
        }
        else
        {
            // Keep ActiveKey so the last hub page stays laid out while parked
            // (visibility hide). Clearing it would display:none-equivalent all pages
            // and reset Embla / scroll state.
            IsHubRouteActive = false;
        }

        NotifyChanged();
    }

    private void TouchMountedKeyUnlocked(FeedHubKey key)
    {
        var existing = _mountedKeys.IndexOf(key);
        if (existing >= 0)
            _mountedKeys.RemoveAt(existing);

        _mountedKeys.Add(key);
    }

    private bool PruneNonHomeKeysUnlocked()
    {
        if (_maxNonHomeKeys is not { } limit)
            return false;

        var removed = false;
        while (CountNonHomeUnlocked() > limit)
        {
            var evictIndex = _mountedKeys.FindIndex(k => k.Kind != FeedHubKind.Home);
            if (evictIndex < 0)
                break;

            _mountedKeys.RemoveAt(evictIndex);
            removed = true;
        }

        return removed;
    }

    private int CountNonHomeUnlocked() =>
        _mountedKeys.Count(k => k.Kind != FeedHubKind.Home);

    private static bool TryGetQueryValue(string query, string key, out string? value)
    {
        value = null;
        if (string.IsNullOrEmpty(query))
            return false;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0 || !string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
                continue;

            value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            return true;
        }

        return false;
    }

    private void NotifyChanged() => Changed?.Invoke();
}
