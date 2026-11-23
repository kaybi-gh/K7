using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Commands.PreExtractSubtitles;

/// <summary>
/// Extracts every text subtitle track of a video file to WebVTT in one read of the file and
/// keeps the result under the metadata root, so playback never waits for an extraction.
/// </summary>
public record PreExtractSubtitlesCommand : IRequest
{
    /// <summary>Background task timeout: the extraction reads the whole media file.</summary>
    public const int TimeoutSeconds = 3600;

    public required Guid Id { get; init; }
}

public class PreExtractSubtitlesCommandHandler(
    IApplicationDbContext context,
    IMediaTranscoder mediaTranscoder,
    IOptions<PathsConfiguration> pathsOptions,
    ILogger<PreExtractSubtitlesCommandHandler> logger) : IRequestHandler<PreExtractSubtitlesCommand>
{
    public async Task Handle(PreExtractSubtitlesCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.IndexedFiles
            .AsNoTracking()
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is not VideoFileMetadata videoMetadata)
            throw new InvalidOperationException("Subtitles can only be pre-extracted for video files with metadata.");

        if (!File.Exists(entity.Path))
            throw new FileNotFoundException("File not found.", entity.Path);

        var metadatasPath = pathsOptions.Value.Metadatas;
        Guard.Against.NullOrEmpty(metadatasPath);

        // The capability may have been switched off while the task was queued.
        var enabled = await context.Libraries
            .AsNoTracking()
            .Where(l => l.Id == entity.LibraryId)
            .Select(l => l.SubtitlePreExtractionEnabled)
            .FirstOrDefaultAsync(cancellationToken);
        if (!enabled)
            return;

        var textTrackIndexes = await context.FileTracks
            .AsNoTracking()
            .OfType<SubtitleFileTrack>()
            .Where(t => t.VideoFileMetadataId == videoMetadata.Id && t.IsTextBased)
            .Select(t => t.Index)
            .ToListAsync(cancellationToken);
        if (textTrackIndexes.Count == 0)
            return;

        var vttCachePathsByTrackIndex = textTrackIndexes
            .Distinct()
            .ToDictionary(
                trackIndex => trackIndex,
                trackIndex => HlsSubtitleVttExtractor.GetPersistentCachePath(metadatasPath, entity.Id, trackIndex));

        await HlsSubtitleVttExtractor.ExtractAllAsync(
            mediaTranscoder,
            entity.Path,
            vttCachePathsByTrackIndex,
            logger,
            cancellationToken);
    }
}
