using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// Remembers the last focused media card per FeedHub page for keyboard focus restore.
/// </summary>
public interface IHubFocusNavigationState
{
    void Save(FeedHubKey pageKey, string mediaId);

    string? GetMediaId(FeedHubKey pageKey);
}
