using System.IO.Pipelines;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Application.Services;
using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetSubtitleSup;

/// <summary>
/// A window of an embedded PGS track as a lossless .sup stream, for clients that draw
/// bitmap subtitles themselves. Windowed so a seek far into a remux never reads the file
/// from the start.
/// </summary>
public record GetSubtitleSupQuery(
    Guid Id,
    int SubtitleTrackIndex,
    double PositionSeconds,
    double DurationSeconds) : IRequest<HttpContentResult>;

public class GetSubtitleSupQueryHandler(
    IApplicationDbContext context,
    IMediaAccessGuard accessGuard,
    IMediaTranscoder mediaTranscoder,
    ILogger<GetSubtitleSupQueryHandler> logger) : IRequestHandler<GetSubtitleSupQuery, HttpContentResult>
{
    public const double MaxWindowSeconds = 600;
    public const string ContentType = "application/octet-stream";

    public async Task<HttpContentResult> Handle(GetSubtitleSupQuery query, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessByIndexedFileAsync(query.Id, cancellationToken);

        var entity = await context.IndexedFiles
            .AsNoTracking()
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        // Remote (federated) files have no local track to copy: those sessions burn in.
        if (entity?.FileMetadata is not VideoFileMetadata videoMetadata || string.IsNullOrEmpty(entity.Path))
            return new EmptyHttpContentResult(404);

        var track = await context.FileTracks
            .OfType<SubtitleFileTrack>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.VideoFileMetadataId == videoMetadata.Id && t.Index == query.SubtitleTrackIndex,
                cancellationToken);
        if (track is null || track.IsTextBased || !BitmapSubtitleCodecs.IsPgs(track.Codec))
            return new EmptyHttpContentResult(404);

        if (!File.Exists(entity.Path))
            return new EmptyHttpContentResult(404);

        if (!double.IsFinite(query.PositionSeconds) || !double.IsFinite(query.DurationSeconds) || query.DurationSeconds <= 0)
            return new EmptyHttpContentResult(400);

        var position = TimeSpan.FromSeconds(Math.Max(0, query.PositionSeconds));
        var duration = TimeSpan.FromSeconds(Math.Min(query.DurationSeconds, MaxWindowSeconds));
        var path = entity.Path;
        var trackIndex = track.Index;

        // ffmpeg writes display sets as it reads the file; the response forwards them as
        // they come so the client draws the first lines before the window is complete.
        // The token is the request's: a client that leaves stops ffmpeg.
        var pipe = new Pipe();
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await using var writer = pipe.Writer.AsStream(leaveOpen: true);
                    await mediaTranscoder.ExtractPgsSubtitleWindowAsync(
                        path, trackIndex, position, duration, writer, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "PGS window extraction failed for file {IndexedFileId} track {Track}",
                        query.Id,
                        trackIndex);
                }
                finally
                {
                    await pipe.Writer.CompleteAsync();
                }
            },
            CancellationToken.None);

        return new StreamHttpContentResult(() => pipe.Reader.AsStream(), ContentType);
    }
}
