using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IFeedHubHostService
{
    event Action? Changed;

    bool IsEnabled { get; }

    bool IsHubRouteActive { get; }

    FeedHubKey? ActiveKey { get; }

    IReadOnlyList<FeedHubKey> MountedKeys { get; }

    void SetEnabled(bool enabled);

    /// <summary>
    /// Max mounted non-Home hub pages. Null means unlimited. Home is never evicted.
    /// </summary>
    void SetMountLimit(int? maxNonHomeKeys);

    bool ShouldDelegateRoute(string uri);

    void UpdateLocation(string uri);
}
