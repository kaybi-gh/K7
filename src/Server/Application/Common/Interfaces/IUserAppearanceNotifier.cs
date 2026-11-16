using K7.Shared.Theming;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients about appearance preference changes for a specific user, so
/// every open device shows them at once.
/// </summary>
public interface IUserAppearanceNotifier
{
    Task NotifyAppearancePreferencesUpdatedAsync(
        string identityUserId,
        AppearancePreferencesDto preferences,
        CancellationToken cancellationToken = default);
}
