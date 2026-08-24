using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for user video player UX settings SignalR updates.
/// </summary>
public interface IUserVideoPlayerSettingsClient
{
    /// <summary>
    /// Receives the effective video player UX settings for the connected user.
    /// </summary>
    Task ReceiveVideoPlayerSettingsUpdated(VideoPlayerSettingsDto settings);
}
