using System.Globalization;
using System.Text;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsVideoStreamSegment;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Extensions;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStream;


public static class GetHlsVideoStreamIndexQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/video/{quality}/index.m3u8";

    public static string Build(GetHlsVideoStreamIndexQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.VideoResolutionIdentifier);

    public static string Build(Guid id, string videoResolutionIdentifier) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", videoResolutionIdentifier);

    public static string BuildManifestRelativePath(string videoResolutionIdentifier) => Route
        .Replace("{id}/hls-stream/", "")
        .Replace("{quality}", $"{videoResolutionIdentifier}");
}
public record GetHlsVideoStreamIndexQuery(
    Guid Id, 
    string VideoResolutionIdentifier,
    Guid StreamSessionId,
    string? TranscodingVideoCodec = null,
    int? SubtitleBurnInStreamIndex = null) : IRequest<IResult>;

public class GetHlsVideoStreamIndexQueryHandler : IRequestHandler<GetHlsVideoStreamIndexQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetHlsVideoStreamIndexQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetHlsVideoStreamIndexQuery query, CancellationToken cancellationToken)
    {
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == query.VideoResolutionIdentifier);
            Guard.Against.Null(quality, nameof(query.VideoResolutionIdentifier), $"Provided quality '{query.VideoResolutionIdentifier}' is not valid.");
        }        

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

        var isTransmuxing = query.VideoResolutionIdentifier == "original"
            && string.IsNullOrEmpty(query.TranscodingVideoCodec)
            && !query.SubtitleBurnInStreamIndex.HasValue;

        var hlsSegments = entity.FileMetadata.GetHlsSegments();

        double[] segmentDurations;
        if (isTransmuxing)
        {
            Guard.Against.NullOrEmpty(hlsSegments);
            segmentDurations = hlsSegments
                .Select(s => s.Duration / 1000.0).ToArray();
        }
        else
        {
            var totalDurationMs = hlsSegments is { Count: > 0 } segments
                ? segments.Sum(s => s.Duration)
                : entity.FileMetadata is VideoFileMetadata v
                    ? (long)v.Duration.TotalMilliseconds
                    : throw new InvalidOperationException("Cannot determine duration for HLS transcoding");

            segmentDurations = ComputeEqualLengthSegments(6000, totalDurationMs);
        }

        var indexPlaylist = GenerateHlsIndexContent(
            segmentDurations,
            query.StreamSessionId,
            query.TranscodingVideoCodec,
            query.SubtitleBurnInStreamIndex);
        return Results.Content(indexPlaylist, "application/vnd.apple.mpegurl");
    }

    private static string GenerateHlsIndexContent(
        double[] segmentDurations,
        Guid streamSessionId,
        string? transcodingVideoCodec,
        int? subtitleBurnInStreamIndex)
    {
        var content = new StringBuilder();
        content.AppendLine("#EXTM3U");
        content.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        // Build query string for segment URLs
        var queryParams = new List<string>
        {
            $"streamSessionId={streamSessionId}"
        };
        
        if (!string.IsNullOrEmpty(transcodingVideoCodec))
            queryParams.Add($"TranscodingVideoCodec={transcodingVideoCodec}");

        if (subtitleBurnInStreamIndex.HasValue)
            queryParams.Add($"SubtitleBurnInStreamIndex={subtitleBurnInStreamIndex.Value}");
            
        var queryString = "?" + string.Join("&", queryParams);
        
        content.AppendLine($"#EXT-X-TARGETDURATION:{Math.Ceiling(segmentDurations.Max())}");
        content.AppendLine("#EXT-X-VERSION:7"); // Version 7 required for fMP4
        content.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        content.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
        content.AppendLine($"#EXT-X-MAP:URI=\"segments/init.m4s{queryString}\"");

        for (int i = 0; i < segmentDurations.Length; i++)
        {
            content.AppendLine($"#EXTINF:{segmentDurations[i].ToString("F6", CultureInfo.InvariantCulture)},");
            content.AppendLine($"{GetHlsVideoStreamSegmentQueryUriBuilder.BuildPlaylistRelativePath(i)}{queryString}");
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
