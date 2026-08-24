using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// SignalR hub events for user video player UX settings (implemented by <see cref="Services.K7HubClient"/>).
/// </summary>
public interface IVideoPlayerSettingsHubEvents
{
    event Action<VideoPlayerSettingsDto>? VideoPlayerSettingsUpdated;
}
