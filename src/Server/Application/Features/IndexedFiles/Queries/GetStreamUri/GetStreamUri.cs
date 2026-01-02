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

public record GetStreamUriQuery() : IRequest<IndexedFileStreamUri>
{
    public required Guid Id { get; set; }
    public Guid? DeviceId { get; set; }
};

public class GetStreamUriQueryHandler : IRequestHandler<GetStreamUriQuery, IndexedFileStreamUri>
{
    private readonly IApplicationDbContext _context;

    public GetStreamUriQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IndexedFileStreamUri> Handle(GetStreamUriQuery query, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(query.DeviceId);

        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        Guard.Against.NotFound(query.Id, indexedFile);

        var device = await _context.Devices
            .FindAsync([query.DeviceId], cancellationToken);
            
        Guard.Against.NotFound((Guid)query.DeviceId, device);

        if (indexedFile.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            throw new NotImplementedException();
        }

        if (indexedFile.FileMetadata is VideoFileMetadata videoFileMetadata)
        {
            return GetVideoFileStreamUri(device, indexedFile, videoFileMetadata);
        }

        throw new InvalidOperationException();
    }

    private static IndexedFileStreamUri GetVideoFileStreamUri(Device device, IndexedFile indexedFile, VideoFileMetadata videoFileMetadata)
    {
        // TODO - Add possibility to manually chose audio track (query param?)
        // TODO - Add user preferences to automatically chose audio track
        var selectedAudioTrack = videoFileMetadata.AudioTracks
            .OrderBy(x => x.IsDefault)
            .FirstOrDefault();

        //var selectedSubtitlesTrack = videoFileMetadata.SubtitleTracks // TODO - Subtitles

        var selectedVideoTrack = videoFileMetadata.VideoTracks.FirstOrDefault();

        var requiresAudioTranscoding = false;
        //var requiresSubtitlesTranscoding = false; // TODO - Subtitles
        var requiresVideoTranscoding = false;
        
        if (selectedAudioTrack != null &&
            !device.SupportedMediaFormats.OfType<AudioMediaFormat>().Any(x => x.Container == videoFileMetadata.Container && x.Codec == selectedAudioTrack.Codec))
        {
            requiresAudioTranscoding = true;
        }

        //if (selectedSubtitlesTrack != null && !device.SupportedVideoCodecs.Contains(selectedVideoTrack.CodecName)) // TODO - Subtitles
        //{
        //    requiresSubtitlesTranscoding = true;
        //}

        if (selectedVideoTrack != null &&
            !device.SupportedMediaFormats.OfType<VideoMediaFormat>().Any(x => x.Container == videoFileMetadata.Container && x.VideoCodec == selectedVideoTrack.Codec))
        {
            requiresVideoTranscoding = true;
        }
        
        if (requiresAudioTranscoding /*|| requiresSubtitlesTranscoding*/ || requiresVideoTranscoding)
        {
            AudioMediaFormat? audioTranscodingMediaFormat = null;
            VideoMediaFormat? videoTranscodingMediaFormat = null;

            if (requiresAudioTranscoding)
            {
                audioTranscodingMediaFormat = GetDeviceBestSupportedAudioMediaFormat([.. device.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);
            }

            if (requiresAudioTranscoding)
            {
                videoTranscodingMediaFormat = GetDeviceBestSupportedVideoMediaFormat([.. device.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Video)]);
            }

            return new()
            {
                Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
                {
                    Id = indexedFile.Id,
                    TranscodingAudioCodec = audioTranscodingMediaFormat?.Codec,
                    TranscodingVideoCodec = videoTranscodingMediaFormat?.VideoCodec
                }), UriKind.Absolute),
                MimeType = "application/vnd.apple.mpegurl"
            };
        }

        return new IndexedFileStreamUri()
        {
            Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Absolute),
            MimeType = Constants.ExtensionFormatMapping[indexedFile.Extension]
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
