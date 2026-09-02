using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Commands.BackfillVideoFrameRate;

public record BackfillVideoFrameRateCommand(Guid IndexedFileId)
    : IRequest<IReadOnlyList<VideoFileTrackDto>>;

public class BackfillVideoFrameRateCommandHandler(
    IApplicationDbContext context,
    IMediaAnalysisService mediaAnalysis,
    ILogger<BackfillVideoFrameRateCommandHandler> logger)
    : IRequestHandler<BackfillVideoFrameRateCommand, IReadOnlyList<VideoFileTrackDto>>
{
    public async Task<IReadOnlyList<VideoFileTrackDto>> Handle(
        BackfillVideoFrameRateCommand request,
        CancellationToken cancellationToken)
    {
        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
                .ThenInclude(x => (x as VideoFileMetadata)!.VideoTracks)
            .FirstOrDefaultAsync(x => x.Id == request.IndexedFileId, cancellationToken);

        Guard.Against.NotFound(request.IndexedFileId, indexedFile);

        if (indexedFile.FileMetadata is not VideoFileMetadata videoMetadata)
            return [];

        var tracks = videoMetadata.VideoTracks
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.Index)
            .ToList();
        if (tracks.Count == 0)
            return [];

        if (tracks.Any(t => !VideoFrameRate.IsMissing(t.FrameRate)))
            return tracks.Select(t => t.ToVideoFileTrackDto()).ToList();

        if (string.IsNullOrWhiteSpace(indexedFile.Path) || !File.Exists(indexedFile.Path))
            return tracks.Select(t => t.ToVideoFileTrackDto()).ToList();

        float? fps;
        try
        {
            fps = await mediaAnalysis.ProbeVideoFrameRateAsync(indexedFile.Path, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to probe video frame rate for indexed file {IndexedFileId}", request.IndexedFileId);
            return tracks.Select(t => t.ToVideoFileTrackDto()).ToList();
        }

        if (VideoFrameRate.IsMissing(fps))
            return tracks.Select(t => t.ToVideoFileTrackDto()).ToList();

        foreach (var track in tracks)
        {
            if (VideoFrameRate.IsMissing(track.FrameRate))
                track.FrameRate = fps;
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Backfilled video frame rate {FrameRate} for indexed file {IndexedFileId}",
            fps,
            request.IndexedFileId);

        return tracks.Select(t => t.ToVideoFileTrackDto()).ToList();
    }
}
