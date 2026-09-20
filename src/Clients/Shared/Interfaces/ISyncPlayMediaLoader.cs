using K7.Clients.Shared.Enums;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.Interfaces;

public interface ISyncPlayMediaLoader
{
    Task LoadAndPlayMediaAsync(
        Guid mediaReferenceId,
        string? title,
        string? coverUrl,
        double? startPosition = null,
        Guid? indexedFileId = null,
        int? audioTrackIndex = null,
        int? subtitleTrackIndex = null,
        double? playbackRate = null,
        AspectRatioMode? aspectRatio = null,
        double? volume = null);

    Task LoadQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> queue, int currentIndex);
}
