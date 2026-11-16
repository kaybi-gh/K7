using K7.Shared.Theming;

namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for user appearance preferences SignalR updates.
/// </summary>
public interface IUserAppearanceClient
{
    /// <summary>
    /// Receives the appearance preferences of the connected user after one of their devices
    /// changed them.
    /// </summary>
    Task ReceiveAppearancePreferencesUpdated(AppearancePreferencesDto preferences);
}
