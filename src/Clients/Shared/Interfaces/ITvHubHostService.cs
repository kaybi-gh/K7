using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ITvHubHostService
{
    event Action? Changed;

    bool IsEnabled { get; }

    bool IsHubRouteActive { get; }

    TvHubKey? ActiveKey { get; }

    IReadOnlyList<TvHubKey> MountedKeys { get; }

    void SetEnabled(bool enabled);

    bool ShouldDelegateRoute(string uri);

    void UpdateLocation(string uri);
}
