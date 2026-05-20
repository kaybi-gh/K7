using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;
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
using K7.Shared.Enums;
using K7.Shared.QueryBuilders;
using Microsoft.Extensions.Logging;

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
    private readonly ISender _sender;
    private readonly IActiveStreamTracker _activeStreamTracker;
    private readonly ILogger<GetStreamUriQueryHandler> _logger;

    public GetStreamUriQueryHandler(
        IApplicationDbContext context,
        IMediaAccessGuard accessGuard,
        ISender sender,
        IActiveStreamTracker activeStreamTracker,
        ILogger<GetStreamUriQueryHandler> logger)
    {
        _context = context;
        _accessGuard = accessGuard;
        _sender = sender;
        _activeStreamTracker = activeStreamTracker;
        _logger = logger;
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

            var (uri, decision) = GetAudioFileStreamUri(device, indexedFile, audioFileMetadata, request);
            _activeStreamTracker.UpdateStreamDecision(request.StreamSessionId, decision);
            return uri;
        }

        if (indexedFile.FileMetadata is VideoFileMetadata videoFileMetadata)
        {
            await _context.Entry(videoFileMetadata)
                .Collection(v => v.AudioTracks)
                .LoadAsync(cancellationToken);

            await _context.Entry(videoFileMetadata)
                .Collection(v => v.VideoTracks)
                .LoadAsync(cancellationToken);

            var hlsSegmentsAvailable = await _context.HlsSegments
                .AnyAsync(s => s.IndexedFileId == request.Id, cancellationToken);

            if (!hlsSegmentsAvailable)
            {
                _logger.LogWarning(
                    "HLS segments not yet computed for IndexedFile {Id}, queuing segmentation and forcing transcoding",
                    request.Id);

                await _sender.Send(new CreateBackgroundTaskCommand
                {
                    Request = new ComputeHlsSegmentsCommand
                    {
                        Id = request.Id,
                        SegmentsDuration = TimeSpan.FromSeconds(2)
                    },
                    Priority = BackgroundTaskPriority.High,
                    TargetEntityId = request.Id,
                    TargetEntityTypeName = nameof(IndexedFile),
                    MaxAttempts = 5,
                    ConcurrencyGroup = "ffmpeg"
                }, cancellationToken);
            }

            var (uri, decision) = GetVideoFileStreamUri(device, indexedFile, videoFileMetadata, request, hlsSegmentsAvailable);
            _activeStreamTracker.UpdateStreamDecision(request.StreamSessionId, decision);
            return uri;
        }

        throw new InvalidOperationException();
    }

    private static (IndexedFileStreamUri Uri, StreamDecisionDto Decision) GetVideoFileStreamUri(Device device, IndexedFile indexedFile, VideoFileMetadata videoFileMetadata, GetStreamUriQuery request, bool hlsSegmentsAvailable)
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

        var sourceResolution = selectedVideoTrack.Width > 0 && selectedVideoTrack.Height > 0
            ? $"{selectedVideoTrack.Width}x{selectedVideoTrack.Height}"
            : null;

        // If both audio and video are directly supported (container + codec), return a direct-stream URL
        if (audioDirectSupported && videoDirectSupported)
        {
            var mimeType = Constants.ContainerMimeTypeMapping.TryGetValue(videoFileMetadata.Container, out var directMime)
                ? directMime
                : "application/octet-stream";

            var decision = new StreamDecisionDto
            {
                Mode = PlaybackMode.Direct,
                SourceVideoCodec = selectedVideoTrack.Codec,
                SourceAudioCodec = selectedAudioTrack.Codec,
                StreamVideoCodec = selectedVideoTrack.Codec,
                StreamAudioCodec = selectedAudioTrack.Codec,
                SourceResolution = sourceResolution,
                SelectedAudioTrackIndex = selectedAudioTrack.Index
            };

            return (new IndexedFileStreamUri
            {
                Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Relative),
                MimeType = mimeType
            }, decision);
        }

        // Otherwise we go through HLS
        var videoCodecSupported = supportedVideoFormats.Any(x => x.VideoCodec == selectedVideoTrack.Codec);

        //var requiresSubtitlesTranscoding = false; // TODO - Subtitles
        var requiresVideoTranscoding = !videoCodecSupported;

        // When HLS segments aren't available, force transcoding to avoid the "original"
        // quality path which requires keyframe-based segments from the database
        var forcedByMissingSegments = !hlsSegmentsAvailable && !requiresVideoTranscoding;
        if (forcedByMissingSegments)
        {
            requiresVideoTranscoding = true;
        }

        VideoMediaFormat? videoTranscodingMediaFormat = null;

        if (requiresVideoTranscoding)
        {
            videoTranscodingMediaFormat = GetDeviceBestSupportedVideoMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Video)]);
        }

        Dictionary<int, string>? audioTrackTranscodings = null;
        // HLS uses fMP4 segments (ISO BMFF / mp4 container), so only audio codecs
        // that the device supports inside mp4 can be stream-copied without transcoding.
        var hlsCompatibleAudioCodecSet = supportedAudioFormats
            .Where(x => x.Container == "mp4")
            .Select(x => x.Codec)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var audioTrack in videoFileMetadata.AudioTracks)
        {
            if (!hlsCompatibleAudioCodecSet.Contains(audioTrack.Codec))
            {
                audioTrackTranscodings ??= [];
                var fallback = GetDeviceBestSupportedAudioMediaFormat([.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);
                audioTrackTranscodings[audioTrack.Index] = fallback.Codec;
            }
        }

        // Build stream decision
        var selectedAudioNeedsTranscode = audioTrackTranscodings?.ContainsKey(selectedAudioTrack.Index) == true;
        var streamAudioCodec = selectedAudioNeedsTranscode
            ? audioTrackTranscodings![selectedAudioTrack.Index]
            : selectedAudioTrack.Codec;

        var reason = BuildVideoTranscodeReason(requiresVideoTranscoding, forcedByMissingSegments, videoCodecSupported, selectedAudioNeedsTranscode);
        var mode = requiresVideoTranscoding ? PlaybackMode.Transcode : PlaybackMode.Transmux;

        var hlsDecision = new StreamDecisionDto
        {
            Mode = mode,
            Reason = reason,
            SourceVideoCodec = selectedVideoTrack.Codec,
            SourceAudioCodec = selectedAudioTrack.Codec,
            StreamVideoCodec = requiresVideoTranscoding ? videoTranscodingMediaFormat?.VideoCodec : selectedVideoTrack.Codec,
            StreamAudioCodec = streamAudioCodec,
            SourceResolution = sourceResolution,
            SelectedAudioTrackIndex = selectedAudioTrack.Index
        };

        return (new IndexedFileStreamUri
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
        }, hlsDecision);
    }

    private static TranscodeReason BuildVideoTranscodeReason(bool requiresVideoTranscoding, bool forcedByMissingSegments, bool videoCodecSupported, bool audioNeedsTranscode)
    {
        var reason = TranscodeReason.None;

        if (requiresVideoTranscoding)
        {
            if (forcedByMissingSegments)
                reason |= TranscodeReason.HlsSegmentsUnavailable;
            else if (!videoCodecSupported)
                reason |= TranscodeReason.VideoCodecNotSupported;
        }

        if (audioNeedsTranscode)
            reason |= TranscodeReason.AudioCodecNotSupported;

        return reason != TranscodeReason.None ? reason : TranscodeReason.ContainerNotSupported;
    }

    // Codecs that work inside fMP4 segments (HLS with ISO BMFF), ordered by preference.
    // MP3 is excluded: MSE does not support MP3 inside fMP4 containers.
    private static readonly string[] HlsFmp4AudioCodecPriority = ["aac", "opus", "ac3", "eac3", "flac", "alac"];

    public static AudioMediaFormat GetDeviceBestSupportedAudioMediaFormat(ICollection<BaseMediaFormat> supportedAudioCodecs)
    {
        var audioFormats = supportedAudioCodecs.OfType<AudioMediaFormat>().ToList();

        foreach (var codec in HlsFmp4AudioCodecPriority)
        {
            var match = audioFormats.FirstOrDefault(f => string.Equals(f.Codec, codec, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return audioFormats.First();
    }

    public static VideoMediaFormat GetDeviceBestSupportedVideoMediaFormat(ICollection<BaseMediaFormat> supportedVideoCodecs)
    {
        return supportedVideoCodecs.OfType<VideoMediaFormat>().First(); // TODO - Implement prioritizing algorithm (cost vs size vs quality)
    }

    private static (IndexedFileStreamUri Uri, StreamDecisionDto Decision) GetAudioFileStreamUri(Device device, IndexedFile indexedFile, AudioFileMetadata audioFileMetadata, GetStreamUriQuery request)
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

            var decision = new StreamDecisionDto
            {
                Mode = PlaybackMode.Direct,
                SourceAudioCodec = audioTrack.Codec,
                StreamAudioCodec = audioTrack.Codec,
                SelectedAudioTrackIndex = audioTrack.Index
            };

            return (new IndexedFileStreamUri
            {
                Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Relative),
                MimeType = mimeType
            }, decision);
        }

        // Transcode via HLS
        var fallbackFormat = GetDeviceBestSupportedAudioMediaFormat(
            [.. device.PlaybackCapabilities.SupportedMediaFormats.Where(x => x.Type == MediaFormatType.Audio)]);

        var transcodeDecision = new StreamDecisionDto
        {
            Mode = PlaybackMode.Transcode,
            Reason = TranscodeReason.AudioCodecNotSupported,
            SourceAudioCodec = audioTrack.Codec,
            StreamAudioCodec = fallbackFormat.Codec,
            SelectedAudioTrackIndex = audioTrack.Index
        };

        return (new IndexedFileStreamUri
        {
            Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
            {
                Id = indexedFile.Id,
                StreamSessionId = request.StreamSessionId,
                AudioTrackTranscodings = new Dictionary<int, string> { [audioTrack.Index] = fallbackFormat.Codec }
            }), UriKind.Relative),
            MimeType = "application/vnd.apple.mpegurl"
        }, transcodeDecision);
    }
}
