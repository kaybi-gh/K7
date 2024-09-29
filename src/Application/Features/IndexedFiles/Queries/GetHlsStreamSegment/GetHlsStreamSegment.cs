using System.Drawing;
using FFMpegCore;
using FFMpegCore.Enums;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;
using MediaServer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsStreamSegment;

public static class GetHlsStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/{quality}/segments/{segmentId}.ts";

    public static string Build(GetHlsStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{quality}", query.VideoResolutionIdentifier)
        .Replace("{segmentId}", $"{query.SegmentId}");

    public static string Build(Guid id, string videoResolutionIdentifier, int segmentId) => Route
        .Replace("{id}", $"{id}")
        .Replace("{quality}", videoResolutionIdentifier)
        .Replace("{segmentId}", $"{segmentId}");

    public static string BuildPlaylistRelativePath(int segmentId) => Route
        .Replace("{id}/hls-stream/{quality}/", "")
        .Replace("{segmentId}", $"{segmentId}");
}

public record GetHlsStreamSegmentQuery(Guid Id, string VideoResolutionIdentifier, int SegmentId) : IRequest<IResult>;

public class GetHlsStreamSegmentQueryHandler : IRequestHandler<GetHlsStreamSegmentQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;

    public GetHlsStreamSegmentQueryHandler(IApplicationDbContext context, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<IResult> Handle(GetHlsStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        if (query.VideoResolutionIdentifier != "original")
        {
            var quality = Qualities.Video.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.VideoResolutionIdentifier), $"Provided quality '{query.VideoResolutionIdentifier}' is not valid.");
        }

        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", query.VideoResolutionIdentifier, $"{query.SegmentId}.ts");
        var file = new FileInfo(segmentPath);

        if (file.Exists)
        {
            return Results.File(file.OpenRead(), contentType: "video/mp2t");
        }

        var segments = await _context.HlsSegments
            .Where(x => x.IndexedFileId == query.Id)
            .Where(x => x.Number >= query.SegmentId)
            .Take(2)
            .ToListAsync(cancellationToken: cancellationToken);

        var indexedFile = await _context.IndexedFiles
            .Where(x => x.Id == segments.First().IndexedFileId)
            .SingleOrDefaultAsync(cancellationToken: cancellationToken);

        Guard.Against.Null(indexedFile);

        var tempDirectory = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", query.VideoResolutionIdentifier, $"{query.SegmentId}");
        if (query.VideoResolutionIdentifier == "original") {
            
            await GenerateRemuxedHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments);
        }
        else
        {
            var quality = Qualities.Video.Where(kvp => kvp.Value.Name == query.VideoResolutionIdentifier).FirstOrDefault();
            await GenerateTranscodedHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments, query.VideoResolutionIdentifier);
        }

        return Results.File(file.OpenRead(), contentType: "video/mp2t");
    }

    private static async Task GenerateRemuxedHlsSegmentAsync(string inputFilePath, string tempDirectory, string outputFilePath, List<HlsSegment> segments)
    {
        Directory.CreateDirectory(tempDirectory);

        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .CopyChannel(Channel.Both))
            .ProcessAsynchronously(throwOnError: true);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static async Task GenerateTranscodedHlsSegmentAsync(string inputFilePath, string tempDirectory, string outputFilePath, List<HlsSegment> segments, string videoResolutionIdentifier)
    {
        Directory.CreateDirectory(tempDirectory);

        var quality = Qualities.Video.Where(kvp => kvp.Value.Name == videoResolutionIdentifier).First().Value;
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
               .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .WithAudioCodec(AudioCodec.Ac3) // TODO - Chose wisely audio codec
                .WithVideoCodec(VideoCodec.LibX264)
                .ConfigureVideoScalingHlsOptions(new Size(quality.Width, quality.Height))) // TODO - Recalculate with ratio
            .ProcessAsynchronously(throwOnError: true);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
}

internal static class FFMpegArgumentsExtensions
{
    public static FFMpegArgumentOptions ConfigureSeekOptions(this FFMpegArgumentOptions options, HlsSegment firstSegment, HlsSegment lastSegment)
    {
        var startTimeStamp = TimeSpan.FromMilliseconds(firstSegment.StartTimestamp);

        return firstSegment != lastSegment
            ? options
                .Seek(startTimeStamp)
                .EndSeek(TimeSpan.FromMilliseconds(lastSegment.StartTimestamp + 10))
            : options
                .Seek(startTimeStamp);
                //.EndSeek(startTimeStamp.Add(TimeSpan.FromMilliseconds(firstSegment.Duration))); // TODO - Useful or not?
    }

    public static FFMpegArgumentOptions ConfigureGenericHlsOptions(this FFMpegArgumentOptions options, string tempDirectory, HlsSegment firstSegment)
    {
        return options
            .WithStartNumber(firstSegment.Number)
            .WithCustomArgument($"-hls_time {firstSegment.Duration}ms")
            .WithCustomArgument("-hls_list_size 0")
            .WithCustomArgument("-copyts")
            .WithCustomArgument($"-hls_segment_filename {Path.Combine(tempDirectory, "%01d.ts")}");
    }

    public static FFMpegArgumentOptions ConfigureVideoScalingHlsOptions(this FFMpegArgumentOptions options, Size? size)
    {
        // TODO - Do we keep this method or not?
        return size is Size targetSize ?
            options.WithVideoFilters(filterOptions => filterOptions.Scale(targetSize))
            : options;
    }
}
