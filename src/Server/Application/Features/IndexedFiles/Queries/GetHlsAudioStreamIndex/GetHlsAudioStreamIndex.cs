using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamIndex;

public static class GetHlsAudioStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audio/{audioTrackIndex}/index.m3u8";

    public static string Build(Guid id, int audioTrackIndex) => Route
        .Replace("{id}", $"{id}")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}");

    /// <summary>
    /// Builds a path relative to the master manifest location for use in #EXT-X-MEDIA URI.
    /// </summary>
    public static string BuildManifestRelativePath(int audioTrackIndex) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{audioTrackIndex}", $"{audioTrackIndex}");
}

public record GetHlsAudioStreamIndexQuery(
    Guid Id,
    int AudioTrackIndex,
    Guid StreamSessionId,
    string? TranscodingAudioCodec = null) : IRequest<IResult>;

public class GetHlsAudioStreamIndexQueryHandler : IRequestHandler<GetHlsAudioStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsAudioStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsAudioStreamIndexQuery query, CancellationToken cancellationToken)
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
            return Results.NotFound();
        }

        var hlsSegments = await HlsSegmentHelper.LoadSegmentsAsync(_context, query.Id, cancellationToken);
        var totalDurationMs = hlsSegments is { Count: > 0 } segments
            ? segments.Sum(s => s.Duration)
            : entity.FileMetadata switch
            {
                VideoFileMetadata v => (long)v.Duration.TotalMilliseconds,
                AudioFileMetadata a => (long)a.Duration.TotalMilliseconds,
                _ => throw new InvalidOperationException("Cannot determine duration for HLS audio stream")
            };

        var indexPlaylist = GenerateHlsAudioIndexContent(
            totalDurationMs,
            query.StreamSessionId,
            query.TranscodingAudioCodec);

        return Results.Content(indexPlaylist, "application/vnd.apple.mpegurl");
    }

    private static string GenerateHlsAudioIndexContent(
        long totalDurationMs,
        Guid streamSessionId,
        string? transcodingAudioCodec)
    {
        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");
        content.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        var segmentDurations = ComputeEqualLengthSegments(6000, totalDurationMs);

        var queryParams = new List<string>
        {
            $"streamSessionId={streamSessionId}"
        };

        if (!string.IsNullOrEmpty(transcodingAudioCodec))
            queryParams.Add($"TranscodingAudioCodec={transcodingAudioCodec}");

        var queryString = "?" + string.Join("&", queryParams);

        content.AppendLine($"#EXT-X-TARGETDURATION:{Math.Ceiling(segmentDurations.Max())}");
        content.AppendLine("#EXT-X-VERSION:7");
        content.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        content.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
        content.AppendLine($"#EXT-X-MAP:URI=\"segments/init.m4s{queryString}\"");

        for (int i = 0; i < segmentDurations.Length; i++)
        {
            content.AppendLine($"#EXTINF:{segmentDurations[i].ToString("F6", CultureInfo.InvariantCulture)},");
            content.AppendLine($"{GetHlsAudioStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath(i)}{queryString}");
        }

        content.AppendLine("#EXT-X-ENDLIST");

        return content.ToString();
    }

    private static double[] ComputeEqualLengthSegments(int desiredSegmentLengthMs, double totalDurationMs)
    {
        if (desiredSegmentLengthMs == 0 || totalDurationMs == 0)
        {
            throw new InvalidOperationException($"Invalid segment length ({desiredSegmentLengthMs}) or duration ({totalDurationMs})");
        }

        var wholeSegments = (int)(totalDurationMs / desiredSegmentLengthMs);
        var remainingMs = totalDurationMs % desiredSegmentLengthMs;

        var segmentsLen = wholeSegments + (remainingMs > 0 ? 1 : 0);
        var segments = new double[segmentsLen];

        for (int i = 0; i < wholeSegments; i++)
        {
            segments[i] = desiredSegmentLengthMs / 1000.0;
        }

        if (remainingMs > 0)
        {
            segments[^1] = remainingMs / 1000.0;
        }

        return segments;
    }
}
