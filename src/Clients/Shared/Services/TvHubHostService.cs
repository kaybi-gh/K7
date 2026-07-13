using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class TvHubHostService : ITvHubHostService
{
    private readonly List<TvHubKey> _mountedKeys = [];
    private readonly object _sync = new();

    public event Action? Changed;

    public bool IsEnabled { get; private set; }

    public bool IsHubRouteActive { get; private set; }

    public TvHubKey? ActiveKey { get; private set; }

    public IReadOnlyList<TvHubKey> MountedKeys
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

    public bool ShouldDelegateRoute(string uri) => IsEnabled && TryParseHubRoute(uri, out _);

    private bool TryParseHubRoute(string uri, out TvHubKey key)
    {
        key = default;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return false;

        var path = absolute.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            path = "/";

        if (path == "/")
        {
            key = TvHubKey.Home;
            return true;
        }

        const string libraryPrefix = "/library-groups/";
        if (path.StartsWith(libraryPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var idSegment = path[libraryPrefix.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (Guid.TryParse(idSegment, out var groupId))
            {
                key = TvHubKey.ForLibraryGroup(groupId);
                return true;
            }
        }

        if (!path.Equals("/explore", StringComparison.OrdinalIgnoreCase))
            return false;

        var query = absolute.Query;
        if (TryGetQueryValue(query, "library-group", out var groupValue)
            && Guid.TryParse(groupValue, out var exploreGroupId))
        {
            key = TvHubKey.ForExploreGroup(exploreGroupId);
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
                if (!_mountedKeys.Contains(key))
                    _mountedKeys.Add(key);
            }

            ActiveKey = key;
            IsHubRouteActive = true;
        }
        else
        {
            ActiveKey = null;
            IsHubRouteActive = false;
        }

        NotifyChanged();
    }

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
