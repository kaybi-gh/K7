using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.MediaFormats;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.QueryBuilders;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;

public record GetStreamUriQuery : IRequest<IndexedFileStreamUri>
{
    public required Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
    public Guid StreamSessionId { get; set; }
};

public class GetStreamUriQueryHandler : IRequestHandler<GetStreamUriQuery, IndexedFileStreamUri>
{
    private readonly IApplicationDbContext _context;

    public GetStreamUriQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IndexedFileStreamUri> Handle(GetStreamUriQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.DeviceId);

        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);

        var device = await _context.Devices
            .FindAsync([request.DeviceId], cancellationToken);
            
        Guard.Against.NotFound((Guid)request.DeviceId, device);

        if (indexedFile.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            throw new NotImplementedException();
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
        // TODO - Add possibility to manually chose audio track (query param?)
        // TODO - Add user preferences to automatically chose audio track
        var selectedAudioTrack = videoFileMetadata.AudioTracks
            .OrderBy(x => x.IsDefault)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Indexed file with id '{indexedFile.Id}' has no audio tracks.");

        //var selectedSubtitlesTrack = videoFileMetadata.SubtitleTracks // TODO - Subtitles

        var selectedVideoTrack = videoFileMetadata.VideoTracks.FirstOrDefault()
            ?? throw new InvalidOperationException($"Indexed file with id '{indexedFile.Id}' has no video tracks.");

        var supportedAudioFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<AudioMediaFormat>().ToList();
        var supportedVideoFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<VideoMediaFormat>().ToList();

        var audioDirectSupported = selectedAudioTrack != null &&
                                   supportedAudioFormats.Any(x => x.Container == videoFileMetadata.Container && x.Codec == selectedAudioTrack.Codec);

        var videoDirectSupported = selectedVideoTrack != null &&
                                   supportedVideoFormats.Any(x => x.Container == videoFileMetadata.Container && x.VideoCodec == selectedVideoTrack.Codec);

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

        // Otherwise we go through HLS; decide what really needs transcoding based on codec support only
        var audioCodecSupported = selectedAudioTrack != null &&
                                  supportedAudioFormats.Any(x => x.Codec == selectedAudioTrack.Codec);

        var videoCodecSupported = selectedVideoTrack != null &&
                                  supportedVideoFormats.Any(x => x.VideoCodec == selectedVideoTrack.Codec);

        var requiresAudioTranscoding = !audioCodecSupported;
        //var requiresSubtitlesTranscoding = false; // TODO - Subtitles
        var requiresVideoTranscoding = !videoCodecSupported;

        AudioMediaFormat? audioTranscodingMediaFormat = null;
        VideoMediaFormat? videoTranscodingMediaFormat = null;

        if (requiresAudioTranscoding)
        {
            audioTranscodingMediaFormat = GetDeviceBestSupportedAudioMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);
        }

        if (requiresVideoTranscoding)
        {
            videoTranscodingMediaFormat = GetDeviceBestSupportedVideoMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Video)]);
        }

        return new()
        {
            Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
            {
                Id = indexedFile.Id,
                StreamSessionId = request.StreamSessionId,
                TranscodingAudioCodec = audioTranscodingMediaFormat?.Codec,
                TranscodingVideoCodec = videoTranscodingMediaFormat?.VideoCodec
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
}
