using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Domain.Interfaces;

public interface IMediaAnalysisService
{
    Task<AudioFileMetadata> GetAudioFileMetadataAsync(string filePath, CancellationToken cancellationToken = default);
    Task<VideoFileMetadata> GetVideoFileMetadataAsync(string filePath, CancellationToken cancellationToken = default);
    Task<List<ChapterMarker>> GetChaptersAsync(string filePath, CancellationToken cancellationToken = default);
    Task<List<HlsSegment>> ComputeKeyframeBasedHlsSegmentsAsync(
        IndexedFile indexedFile,
        TimeSpan segmentsDuration,
        long totalVideoDuration,
        CancellationToken cancellationToken = default
    );
    Task<MetadataPicture> GenerateThumbnailsAsync(IndexedFile indexedFile, int delayBetweenTilesInSeconds = 30, CancellationToken cancellationToken = default);
}
