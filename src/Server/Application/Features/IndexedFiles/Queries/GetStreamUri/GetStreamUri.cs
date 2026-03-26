using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.QueryBuilders;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;

public record GetStreamUriQuery : IRequest<IndexedFileStreamUri>
{
    public required Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid StreamSessionId { get; set; }
    public int? AudioTrackIndex { get; set; }
};

public class GetStreamUriQueryHandler : IRequestHandler<GetStreamUriQuery, IndexedFileStreamUri>
{
    private readonly IApplicationDbContext _context;
    private readonly IMediaAccessGuard _accessGuard;

    public GetStreamUriQueryHandler(IApplicationDbContext context, IMediaAccessGuard accessGuard)
    {
        _context = context;
        _accessGuard = accessGuard;
    }

    public async Task<IndexedFileStreamUri> Handle(GetStreamUriQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.DeviceId);

        await _accessGuard.EnsureAccessByIndexedFileAsync(request.Id, cancellationToken);

        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);

        var device = await _context.Devices
            .FindAsync([request.DeviceId], cancellationToken);
            
        Guard.Against.NotFound((Guid)request.DeviceId, device);

        if (indexedFile.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            await _context.Entry(audioFileMetadata)
                .Reference(a => a.AudioTrack)
                .LoadAsync(cancellationToken);

            return GetAudioFileStreamUri(device, indexedFile, audioFileMetadata, request);
        }

        if (indexedFile.FileMetadata is VideoFileMetadata videoFileMetadata)
        {
            await _context.Entry(videoFileMetadata)
                .Collection(v => v.AudioTracks)
                .LoadAsync(cancellationToken);

            await _context.Entry(videoFileMetadata)
                .Collection(v => v.VideoTracks)
                .LoadAsync(cancellationToken);

            return GetVideoFileStreamUri(device, indexedFile, videoFileMetadata, request);
        }

        throw new InvalidOperationException();
    }

    private static IndexedFileStreamUri GetVideoFileStreamUri(Device device, IndexedFile indexedFile, VideoFileMetadata videoFileMetadata, GetStreamUriQuery request)
    {
        AudioFileTrack selectedAudioTrack;
        if (request.AudioTrackIndex is int audioIdx)
        {
            selectedAudioTrack = videoFileMetadata.AudioTracks.FirstOrDefault(t => t.Index == audioIdx)
                ?? throw new InvalidOperationException($"Audio track index '{audioIdx}' not found for indexed file '{indexedFile.Id}'.");
        }
        else
        {
            selectedAudioTrack = videoFileMetadata.AudioTracks
                .OrderByDescending(x => x.IsDefault)
                .FirstOrDefault()
                ?? throw new InvalidOperationException($"Indexed file with id '{indexedFile.Id}' has no audio tracks.");
        }

        //var selectedSubtitlesTrack = videoFileMetadata.SubtitleTracks // TODO - Subtitles

        var selectedVideoTrack = videoFileMetadata.VideoTracks.FirstOrDefault()
            ?? throw new InvalidOperationException($"Indexed file with id '{indexedFile.Id}' has no video tracks.");

        var supportedAudioFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<AudioMediaFormat>().ToList();
        var supportedVideoFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<VideoMediaFormat>().ToList();

        var audioDirectSupported = supportedAudioFormats.Any(x => x.Container == videoFileMetadata.Container && x.Codec == selectedAudioTrack.Codec);

        var videoDirectSupported = supportedVideoFormats.Any(x => x.Container == videoFileMetadata.Container && x.VideoCodec == selectedVideoTrack.Codec);

        // If both audio and video are directly supported (container + codec), return a direct-stream URL
        if (audioDirectSupported && videoDirectSupported)
        {
            var mimeType = Constants.ContainerMimeTypeMapping.TryGetValue(videoFileMetadata.Container, out var directMime)
                ? directMime
                : "application/octet-stream";

            return new IndexedFileStreamUri
            {
                Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Relative),
                MimeType = mimeType
            };
        }

        // Otherwise we go through HLS
        var videoCodecSupported = supportedVideoFormats.Any(x => x.VideoCodec == selectedVideoTrack.Codec);

        //var requiresSubtitlesTranscoding = false; // TODO - Subtitles
        var requiresVideoTranscoding = !videoCodecSupported;

        VideoMediaFormat? videoTranscodingMediaFormat = null;

        if (requiresVideoTranscoding)
        {
            videoTranscodingMediaFormat = GetDeviceBestSupportedVideoMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Video)]);
        }

        Dictionary<int, string>? audioTrackTranscodings = null;
        var supportedAudioCodecSet = supportedAudioFormats.Select(x => x.Codec).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var audioTrack in videoFileMetadata.AudioTracks)
        {
            if (!supportedAudioCodecSet.Contains(audioTrack.Codec))
            {
                audioTrackTranscodings ??= [];
                var fallback = GetDeviceBestSupportedAudioMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);
                audioTrackTranscodings[audioTrack.Index] = fallback.Codec;
            }
        }

        return new()
        {
            Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
            {
                Id = indexedFile.Id,
                StreamSessionId = request.StreamSessionId,
                TranscodingVideoCodec = videoTranscodingMediaFormat?.VideoCodec,
                AudioTrackTranscodings = audioTrackTranscodings,
                DefaultAudioTrackIndex = request.AudioTrackIndex
            }), UriKind.Relative),
            MimeType = "application/vnd.apple.mpegurl"
        };
    }

    public static AudioMediaFormat GetDeviceBestSupportedAudioMediaFormat(ICollection<BaseMediaFormat> supportedAudioCodecs)
    {
        return supportedAudioCodecs.OfType<AudioMediaFormat>().First(); // TODO - Implement prioritizing algorithm (cost vs size vs quality)
    }

    public static VideoMediaFormat GetDeviceBestSupportedVideoMediaFormat(ICollection<BaseMediaFormat> supportedVideoCodecs)
    {
        return supportedVideoCodecs.OfType<VideoMediaFormat>().First(); // TODO - Implement prioritizing algorithm (cost vs size vs quality)
    }

    private static IndexedFileStreamUri GetAudioFileStreamUri(Device device, IndexedFile indexedFile, AudioFileMetadata audioFileMetadata, GetStreamUriQuery request)
    {
        var audioTrack = audioFileMetadata.AudioTrack
            ?? throw new InvalidOperationException($"Indexed file '{indexedFile.Id}' has no audio track metadata.");

        var supportedAudioFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<AudioMediaFormat>().ToList();

        var directSupported = supportedAudioFormats.Any(x =>
            x.Container == audioFileMetadata.Container && x.Codec == audioTrack.Codec);

        if (directSupported)
        {
            var mimeType = Constants.ContainerMimeTypeMapping.TryGetValue(audioFileMetadata.Container, out var mime)
                ? mime
                : "application/octet-stream";

            return new IndexedFileStreamUri
            {
                Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Relative),
                MimeType = mimeType
            };
        }

        // Transcode via HLS
        var fallbackFormat = GetDeviceBestSupportedAudioMediaFormat(
            [.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);

        return new IndexedFileStreamUri
        {
            Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
            {
                Id = indexedFile.Id,
                StreamSessionId = request.StreamSessionId,
                AudioTrackTranscodings = new Dictionary<int, string> { [audioTrack.Index] = fallbackFormat.Codec }
            }), UriKind.Relative),
            MimeType = "application/vnd.apple.mpegurl"
        };
    }
}
