using K7.Shared.Dtos;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients about video player UX settings changes for a specific user.
/// </summary>
public interface IUserVideoPlayerSettingsNotifier
{
    Task NotifyVideoPlayerSettingsUpdatedAsync(
        string identityUserId,
        VideoPlayerSettingsDto settings,
        CancellationToken cancellationToken = default);
}
