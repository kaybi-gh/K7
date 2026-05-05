using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Interfaces;
using K7.Server.Application.Common.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSubtitleStreamSegment;

public static class GetHlsSubtitleStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/subtitles/{subtitleTrackIndex}/segments/{segmentNumber}.vtt";

    public static string Build(Guid id, int subtitleTrackIndex, int segmentNumber) => Route
        .Replace("{id}", $"{id}")
        .Replace("{subtitleTrackIndex}", $"{subtitleTrackIndex}")
        .Replace("{segmentNumber}", segmentNumber.ToString());

    public static string BuildPlaylistRelativePath(int segmentNumber) =>
        $"segments/{segmentNumber}.vtt";
}

public record GetHlsSubtitleStreamSegmentQuery(
    Guid Id,
    int SubtitleTrackIndex,
    int SegmentNumber,
    Guid StreamSessionId) : IRequest<IResult>;

public class GetHlsSubtitleStreamSegmentQueryHandler : IRequestHandler<GetHlsSubtitleStreamSegmentQuery, IResult>
{
    private const int SubtitleSegmentDurationSeconds = 30;

    private readonly IApplicationDbContext _context;
    private readonly IMediaTranscoder _mediaTranscoder;
    private readonly ILogger<GetHlsSubtitleStreamSegmentQueryHandler> _logger;
    private readonly string _transcodingPath;

    // Simple in-memory lock to prevent concurrent extraction of the same subtitle track
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _extractionLocks = new();

    public GetHlsSubtitleStreamSegmentQueryHandler(
        IApplicationDbContext context,
        IMediaTranscoder mediaTranscoder,
        ILogger<GetHlsSubtitleStreamSegmentQueryHandler> logger,
        IOptions<PathsConfiguration> pathsOptions)
    {
        _context = context;
        _mediaTranscoder = mediaTranscoder;
        _logger = logger;
        _transcodingPath = pathsOptions.Value.Transcoding
            ?? throw new InvalidOperationException("Transcoding path not configured");
    }

    public async Task<IResult> Handle(GetHlsSubtitleStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, entity);
        Guard.Against.NullOrEmpty(entity.Path);
        Guard.Against.Null(entity.FileMetadata);

        var file = new FileInfo(entity.Path);
        if (!file.Exists)
        {
            return Results.NotFound("Source file not found");
        }

        // Ensure the full VTT is extracted and cached
        var vttCachePath = Path.Combine(
            _transcodingPath,
            entity.Id.ToString("N"),
            "subtitles",
            $"{query.SubtitleTrackIndex}.vtt");

        await EnsureVttExtractedAsync(entity.Path, query.SubtitleTrackIndex, vttCachePath, cancellationToken);

        if (!File.Exists(vttCachePath))
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        // Read the full VTT and extract the segment for the requested time range
        var fullVtt = await File.ReadAllTextAsync(vttCachePath, cancellationToken);
        var startTimeSeconds = query.SegmentNumber * SubtitleSegmentDurationSeconds;
        var endTimeSeconds = startTimeSeconds + SubtitleSegmentDurationSeconds;

        var segmentVtt = WebVttSegmenter.ExtractSegment(fullVtt, startTimeSeconds, endTimeSeconds);

        return Results.Content(segmentVtt, "text/vtt; charset=utf-8");
    }

    private async Task EnsureVttExtractedAsync(string inputPath, int trackIndex, string vttCachePath, CancellationToken cancellationToken)
    {
        if (File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length > 0)
        {
            return;
        }

        var lockKey = vttCachePath;
        var semaphore = _extractionLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length > 0)
            {
                return;
            }

            // Delete any stale 0-byte file from a previous failed extraction
            if (File.Exists(vttCachePath))
            {
                File.Delete(vttCachePath);
            }

            await _mediaTranscoder.ExtractSubtitleAsVttAsync(inputPath, trackIndex, vttCachePath, cancellationToken);

            // Clean up if ffmpeg produced an empty file
            if (File.Exists(vttCachePath) && new FileInfo(vttCachePath).Length == 0)
            {
                _logger.LogWarning(
                    "FFmpeg produced empty VTT for track {Track} from {Input} - removing cached file",
                    trackIndex, inputPath);
                File.Delete(vttCachePath);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }
}

/// <summary>
/// Utility to extract time-range segments from a full WebVTT file.
/// Produces a valid WebVTT segment with X-TIMESTAMP-MAP header for HLS sync.
/// </summary>
internal static class WebVttSegmenter
{
    /// <summary>
    /// Extracts cues that overlap the given time range and returns a valid WebVTT segment.
    /// </summary>
    public static string ExtractSegment(string fullVttContent, double startTimeSeconds, double endTimeSeconds)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        // MPEGTS:0 because our fMP4 video segments start with PTS 0 (EXT-X-VERSION:7)
        sb.AppendLine("X-TIMESTAMP-MAP=MPEGTS:0,LOCAL:00:00:00.000");
        sb.AppendLine();

        var lines = fullVttContent.Split('\n');
        var i = 0;

        // Skip header (WEBVTT line and any lines before first cue)
        while (i < lines.Length)
        {
            var line = lines[i].Trim();
            if (line.Contains("-->"))
            {
                break;
            }
            i++;
        }

        // Parse and filter cues
        while (i < lines.Length)
        {
            var line = lines[i].Trim();

            // Check if this line is a cue timing line
            if (line.Contains("-->"))
            {
                var (cueStart, cueEnd) = ParseCueTiming(line);

                // Include cue if it overlaps with our segment time range
                if (cueStart < endTimeSeconds && cueEnd > startTimeSeconds)
                {
                    // Include the timing line
                    sb.AppendLine(line);
                    i++;

                    // Include all cue payload lines until blank line or end
                    while (i < lines.Length && lines[i].Trim().Length > 0)
                    {
                        sb.AppendLine(lines[i].TrimEnd());
                        i++;
                    }
                    sb.AppendLine(); // Blank line separator
                }
                else
                {
                    // Skip this cue
                    i++;
                    while (i < lines.Length && lines[i].Trim().Length > 0)
                    {
                        i++;
                    }
                }
            }
            else
            {
                // Skip non-timing lines (cue identifiers, blank lines)
                i++;
            }
        }

        return sb.ToString();
    }

    private static (double Start, double End) ParseCueTiming(string timingLine)
    {
        // Format: "00:01:23.456 --> 00:01:26.789" or "01:23.456 --> 01:26.789"
        var parts = timingLine.Split("-->");
        if (parts.Length != 2)
        {
            return (0, 0);
        }

        // The end part may have position/alignment settings after the timestamp
        var endPart = parts[1].Trim();
        var endSpaceIndex = endPart.IndexOf(' ');
        if (endSpaceIndex > 0)
        {
            endPart = endPart[..endSpaceIndex];
        }

        return (ParseVttTimestamp(parts[0].Trim()), ParseVttTimestamp(endPart));
    }

    private static double ParseVttTimestamp(string timestamp)
    {
        // Supports "HH:MM:SS.mmm" and "MM:SS.mmm"
        var parts = timestamp.Split(':');
        try
        {
            return parts.Length switch
            {
                3 => double.Parse(parts[0], CultureInfo.InvariantCulture) * 3600
                   + double.Parse(parts[1], CultureInfo.InvariantCulture) * 60
                   + double.Parse(parts[2], CultureInfo.InvariantCulture),
                2 => double.Parse(parts[0], CultureInfo.InvariantCulture) * 60
                   + double.Parse(parts[1], CultureInfo.InvariantCulture),
                _ => 0
            };
        }
        catch
        {
            return 0;
        }
    }
}
