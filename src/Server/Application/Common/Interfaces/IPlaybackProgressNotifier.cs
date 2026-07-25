using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients about playback progress changes for a specific user.
/// </summary>
public interface IPlaybackProgressNotifier
{
    Task NotifyProgressUpdatedAsync(
        string identityUserId,
        Guid mediaId,
        double progressPercentage,
        bool isCompleted,
        MediaType mediaType,
        CancellationToken cancellationToken = default);
}
