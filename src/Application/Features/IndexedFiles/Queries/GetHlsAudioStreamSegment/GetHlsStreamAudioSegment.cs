using System.Collections.Concurrent;
using FFMpegCore;
using FFMpegCore.Enums;
using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;
using MediaServer.Infrastructure.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using AudioQuality = MediaServer.Domain.Constants.AudioQuality;

namespace MediaServer.Application.Features.IndexedFiles.Queries.GetHlsAudioStreamSegment;

public static class GetHlsAudioStreamSegmentQueryUriBuilder
{
    public const string Route = "{id}/hls-stream/audios/{index}/{quality}/segments/{segmentId}.ts";

    public static string Build(GetHlsAudioStreamSegmentQuery query) => Route
        .Replace("{id}", $"{query.Id}")
        .Replace("{index}", $"{query.Index}")
        .Replace("{quality}", query.AudioQualityIdentifier)
        .Replace("{segmentId}", $"{query.SegmentId}");
    
    // TODO - Use Build with original query params
    public static string Build(Guid id, int index, string audioQualityIdentifier, int segmentId) => Route
        .Replace("{id}", $"{id}")
        .Replace("{index}", $"{index}")
        .Replace("{quality}", audioQualityIdentifier)
        .Replace("{segmentId}", $"{segmentId}");

    public static string BuildPlaylistRelativePath(int segmentId) => Route
        .Replace("{id}/hls-stream/audios/{index}/{quality}/", "")
        .Replace("{segmentId}", $"{segmentId}");
}

public record GetHlsAudioStreamSegmentQuery(Guid Id, int Index, string AudioQualityIdentifier, int SegmentId) : IRequest<IResult>;

public class GetHlsAudioStreamSegmentQueryHandler : IRequestHandler<GetHlsAudioStreamSegmentQuery, IResult>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _segmentLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
    private readonly IApplicationDbContext _context;
    private readonly PathsConfiguration _pathsConfiguration;

    public GetHlsAudioStreamSegmentQueryHandler(IApplicationDbContext context, IOptions<PathsConfiguration> pathsConfiguration)
    {
        _context = context;
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<IResult> Handle(GetHlsAudioStreamSegmentQuery query, CancellationToken cancellationToken)
    {
        // TODO - Create specific enum
        if (query.AudioQualityIdentifier != "original")
        {
            var quality = Qualities.Video.Where(kvp => kvp.Value.Name == query.AudioQualityIdentifier).FirstOrDefault();
            Guard.Against.Null(quality, nameof(query.AudioQualityIdentifier), $"Provided quality '{query.AudioQualityIdentifier}' is not valid.");
        }

        var segmentPath = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "audio", $"{query.Index}", query.AudioQualityIdentifier, $"{query.SegmentId}.ts");

        var file = new FileInfo(segmentPath);
        if (file.Exists)
        {
            return Results.File(file.OpenRead(), contentType: "video/mp2t");
        }

        var semaphore = _segmentLocks.GetOrAdd(segmentPath, new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(0, cancellationToken))
        {
            return Results.Conflict("Segment generation already in progress.");
        }

        try
        {
            var segments = await _context.HlsSegments
                .Where(x => x.IndexedFileId == query.Id)
                .Where(x => x.Number >= query.SegmentId)
                .Take(2)
                .ToListAsync(cancellationToken: cancellationToken);

            var indexedFile = await _context.IndexedFiles
                .Where(x => x.Id == segments.First().IndexedFileId)
                .SingleOrDefaultAsync(cancellationToken: cancellationToken);

            // Check if file is video?
            Guard.Against.Null(indexedFile);

            var tempDirectory = Path.Combine(_pathsConfiguration.Transcoding, $"{query.Id}", "audio", $"{query.Index}", query.AudioQualityIdentifier, $"{query.SegmentId}");
            Directory.CreateDirectory(tempDirectory);

            if (query.AudioQualityIdentifier == "original")
            {
                await GenerateRemuxedHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments, query.Index);
            }
            else
            {
                var quality = Qualities.Audio.Where(kvp => kvp.Value.Name == query.AudioQualityIdentifier).FirstOrDefault();
                await GenerateTranscodedHlsSegmentAsync(indexedFile.Path, tempDirectory, segmentPath, segments, query.Index, quality.Value);
            }
        }
        finally
        {
            semaphore.Release();
            _segmentLocks.TryRemove(segmentPath, out _);
        }

        return Results.File(file.OpenRead(), contentType: "video/mp2t");
    }

    private static async Task GenerateRemuxedHlsSegmentAsync(string inputFilePath, string tempDirectory, string outputFilePath, List<HlsSegment> segments, int fileStreamIndex)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(fileStreamIndex)
                .CopyChannel(Channel.Audio))
            .ProcessAsynchronously(throwOnError: true);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    private static async Task GenerateTranscodedHlsSegmentAsync(string inputFilePath, string tempDirectory, string outputFilePath, List<HlsSegment> segments, int fileStreamIndex, AudioQuality audioQuality)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
               .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(fileStreamIndex)
                .WithAudioCodec(AudioCodec.Aac)
                .WithCustomArgument($"-ac  6"))
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
}
