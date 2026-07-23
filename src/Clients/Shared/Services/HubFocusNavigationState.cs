using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

public sealed class HubFocusNavigationState : IHubFocusNavigationState
{
    private readonly Dictionary<FeedHubKey, string> _byPage = [];
    private readonly object _sync = new();

    public void Save(FeedHubKey pageKey, string mediaId)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
            return;

        lock (_sync)
            _byPage[pageKey] = mediaId;
    }

    public string? GetMediaId(FeedHubKey pageKey)
    {
        lock (_sync)
            return _byPage.TryGetValue(pageKey, out var mediaId) ? mediaId : null;
    }
}
