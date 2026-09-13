using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleVtt;

public record GetSubtitleVttQuery(Guid Id, int SubtitleTrackIndex) : IRequest<HttpContentResult>;

public class GetSubtitleVttQueryHandler : IRequestHandler<GetSubtitleVttQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAccessGuard _accessGuard;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly ILogger<GetSubtitleVttQueryHandler> _logger;
    private readonly string _transcodingPath;

    public GetSubtitleVttQueryHandler(
        IApplicationDbContext context,
        IMediaAccessGuard accessGuard,
        IMediaTranscoder mediaTranscoder,
        ILogger<GetSubtitleVttQueryHandler> logger,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _context = context;
        _accessGuard = accessGuard;
        _mediaTranscoder = mediaTranscoder;
        _logger = logger;
        _transcodingPath = pathsOptions.Value.Transcoding
            ?? throw new InvalidOperationException("Transcoding path not configured");
    }

    public async Task<HttpContentResult> Handle(GetSubtitleVttQuery query, CancellationToken cancellationToken)
    {
        await _accessGuard.EnsureAccessByIndexedFileAsync(query.Id, cancellationToken);

        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);

        if (entity.FileMetadata is not VideoFileMetadata videoMetadata)
            return new EmptyHttpContentResult(404);

        await _context.Entry(videoMetadata).Collection(v => v.SubtitleTracks).LoadAsync(cancellationToken);

        var track = videoMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == query.SubtitleTrackIndex);
        if (track is null || !track.IsTextBased)
            return new EmptyHttpContentResult(404);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
            return new EmptyHttpContentResult(404);

        var vttCachePath = HlsSubtitleVttExtractor.GetCachePath(
            _transcodingPath,
            entity.Id,
            query.SubtitleTrackIndex);

        if (!HlsSubtitleVttExtractor.IsReady(vttCachePath))
        {
            // Never block: extract in the background and let the client poll. Blocking here
            // makes Android/web wait on ffmpeg before playback and stalls the request thread.
            _logger.LogDebug(
                "Subtitle VTT cache miss for file {IndexedFileId} track {Track} - extract in background",
                entity.Id,
                query.SubtitleTrackIndex);
            HlsSubtitleVttExtractor.StartBackgroundExtract(
                _mediaTranscoder,
                entity.Path,
                query.SubtitleTrackIndex,
                vttCachePath,
                _logger);
            return new TextHttpContentResult(
                "Subtitle extract in progress",
                "text/plain",
                503);
        }

        var vtt = await File.ReadAllTextAsync(vttCachePath, cancellationToken);
        return new TextHttpContentResult(vtt, "text/vtt; charset=utf-8");
    }
}
