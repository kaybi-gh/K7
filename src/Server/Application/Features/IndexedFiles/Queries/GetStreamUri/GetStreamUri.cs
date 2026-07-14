using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.IndexedFiles.Queries.GetHlsStreamManifest;
using K7.Server.Application.Features.TrackSelectionPreferences.Queries.GetEffectiveTrackSelectionPreferences;
using K7.Server.Application.Helpers;
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
    public int? SubtitleTrackIndex { get; set; }
};

public class GetStreamUriQueryHandler(
    IApplicationDbContext context,
    IMediaAccessGuard accessGuard,
    ISender sender,
    IActiveStreamTracker activeStreamTracker,
    IFfmpegCapabilitiesService ffmpegCapabilitiesService,
    ILogger<GetStreamUriQueryHandler> logger) : IRequestHandler<GetStreamUriQuery, IndexedFileStreamUri>
{
    public async Task<IndexedFileStreamUri> Handle(GetStreamUriQuery request, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(request.DeviceId);

        await accessGuard.EnsureAccessByIndexedFileAsync(request.Id, cancellationToken);

        var indexedFile = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);

        var device = await context.Devices
            .FindAsync([request.DeviceId], cancellationToken);

        Guard.Against.NotFound((Guid)request.DeviceId, device);

        if (indexedFile.FileMetadata is AudioFileMetadata audioFileMetadata)
        {
            await context.Entry(audioFileMetadata)
                .Reference(a => a.AudioTrack)
                .LoadAsync(cancellationToken);

            var (uri, decision) = GetAudioFileStreamUri(device, indexedFile, audioFileMetadata, request);
            activeStreamTracker.UpdateStreamDecision(request.StreamSessionId, decision);
            return uri;
        }

        if (indexedFile.FileMetadata is VideoFileMetadata videoFileMetadata)
        {
            await context.Entry(videoFileMetadata)
                .Collection(v => v.AudioTracks)
                .LoadAsync(cancellationToken);

            await context.Entry(videoFileMetadata)
                .Collection(v => v.VideoTracks)
                .LoadAsync(cancellationToken);

            await context.Entry(videoFileMetadata)
                .Collection(v => v.SubtitleTracks)
                .LoadAsync(cancellationToken);

            var hlsSegmentsAvailable = await HlsSegmentHelper.HasSegmentsAsync(context, request.Id, cancellationToken);

            if (!hlsSegmentsAvailable)
            {
                await HlsSegmentHelper.QueueSegmentComputationIfMissingAsync(
                    sender,
                    request.Id,
                    logger,
                    cancellationToken);
            }

            var subtitleTrackIndex = request.SubtitleTrackIndex;
            if (request.AudioTrackIndex is null)
            {
                var preferences = await sender.Send(
                    new GetEffectiveTrackSelectionPreferencesQuery { LibraryId = indexedFile.LibraryId },
                    cancellationToken);

                var audioDtos = videoFileMetadata.AudioTracks
                    .OrderBy(t => t.Index)
                    .Select(t => t.ToAudioFileTrackDto())
                    .ToList();

                var subtitleDtos = videoFileMetadata.SubtitleTracks
                    .OrderBy(t => t.Index)
                    .Select(t => t.ToSubtitleFileTrackDto())
                    .ToList();

                var selection = TrackSelector.SelectTracks(preferences, audioDtos, subtitleDtos);
                request.AudioTrackIndex = selection.AudioTrackIndex;
                subtitleTrackIndex ??= selection.SubtitleTrackIndex;
                request.SubtitleTrackIndex = subtitleTrackIndex;
            }

            var (uri, decision) = GetVideoFileStreamUri(device, indexedFile, videoFileMetadata, request, hlsSegmentsAvailable, subtitleTrackIndex);
            decision = await StreamDecisionEnrichment.EnrichEncodersAsync(decision, ffmpegCapabilitiesService, cancellationToken);
            activeStreamTracker.UpdateStreamDecision(request.StreamSessionId, decision);
            return uri;
        }

        throw new InvalidOperationException(
            $"Indexed file '{indexedFile.Id}' has unsupported metadata type '{indexedFile.FileMetadata?.GetType().Name ?? "null"}'.");
    }

    public static (IndexedFileStreamUri Uri, StreamDecisionDto Decision) GetVideoFileStreamUri(Device device, IndexedFile indexedFile, VideoFileMetadata videoFileMetadata, GetStreamUriQuery request, bool hlsSegmentsAvailable, int? subtitleTrackIndex)
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

        var selectedVideoTrack = videoFileMetadata.VideoTracks.FirstOrDefault()
            ?? throw new InvalidOperationException($"Indexed file with id '{indexedFile.Id}' has no video tracks.");

        var supportedAudioFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<AudioMediaFormat>().ToList();
        var supportedVideoFormats = device.PlaybackCapabilities.SupportedMediaFormats.OfType<VideoMediaFormat>().ToList();

        var audioDirectSupported = supportedAudioFormats.Any(x => x.Container == videoFileMetadata.Container && x.Codec == selectedAudioTrack.Codec);

        var videoDirectSupported = supportedVideoFormats.Any(x => x.Container == videoFileMetadata.Container && x.VideoCodec == selectedVideoTrack.Codec);

        var sourceResolution = selectedVideoTrack.Width > 0 && selectedVideoTrack.Height > 0
            ? $"{selectedVideoTrack.Width}x{selectedVideoTrack.Height}"
            : null;

        var selectedSubtitle = subtitleTrackIndex.HasValue
            ? videoFileMetadata.SubtitleTracks.FirstOrDefault(t => t.Index == subtitleTrackIndex.Value)
            : null;

        var subtitleBurnInStreamIndex = selectedSubtitle is { IsTextBased: false } ? selectedSubtitle.Index : (int?)null;
        var defaultTextSubtitleTrackIndex = selectedSubtitle is { IsTextBased: true } ? selectedSubtitle.Index : (int?)null;

        var resolutionExceedsDevice = device.DisplayHeight > 0
            && selectedVideoTrack.Height > 0
            && selectedVideoTrack.Height > device.DisplayHeight;

        // If both audio and video are directly supported (container + codec), return a direct-stream URL
        if (audioDirectSupported && videoDirectSupported && !resolutionExceedsDevice)
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
                SelectedAudioTrackIndex = selectedAudioTrack.Index,
                AudioTrackLanguage = selectedAudioTrack.Language,
                AudioTrackTitle = selectedAudioTrack.Name,
                AudioChannelLayout = selectedAudioTrack.ChannelLayout,
                SubtitleTrackLanguage = selectedSubtitle?.Language,
                SubtitleTrackTitle = selectedSubtitle?.Name,
                SubtitleCodec = selectedSubtitle?.Codec,
                SelectedSubtitleTrackIndex = selectedSubtitle?.Index,
                IsSubtitleBurnIn = subtitleBurnInStreamIndex.HasValue
            };

            return (new IndexedFileStreamUri
            {
                Uri = new Uri(GetIndexedFileDirectStreamQueryUriBuilder.Build(indexedFile.Id), UriKind.Relative),
                MimeType = mimeType
            }, decision);
        }

        // Otherwise we go through HLS
        var videoCodecSupported = supportedVideoFormats.Any(x => x.VideoCodec == selectedVideoTrack.Codec);

        var requiresVideoTranscoding = !videoCodecSupported || resolutionExceedsDevice;

        // When HLS segments aren't available, force transcoding to avoid the "original"
        // quality path which requires keyframe-based segments from the database
        var forcedByMissingSegments = !hlsSegmentsAvailable && !requiresVideoTranscoding;
        if (forcedByMissingSegments)
        {
            requiresVideoTranscoding = true;
        }

        VideoMediaFormat? videoTranscodingMediaFormat = null;

        if (subtitleBurnInStreamIndex.HasValue)
        {
            requiresVideoTranscoding = true;
        }

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

        var reason = BuildVideoTranscodeReason(
            requiresVideoTranscoding,
            forcedByMissingSegments,
            videoCodecSupported,
            selectedAudioNeedsTranscode,
            subtitleBurnInStreamIndex.HasValue,
            resolutionExceedsDevice);
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
            SelectedAudioTrackIndex = selectedAudioTrack.Index,
            AudioTrackLanguage = selectedAudioTrack.Language,
            AudioTrackTitle = selectedAudioTrack.Name,
            AudioChannelLayout = selectedAudioTrack.ChannelLayout,
            SubtitleTrackLanguage = selectedSubtitle?.Language,
            SubtitleTrackTitle = selectedSubtitle?.Name,
            SubtitleCodec = selectedSubtitle?.Codec,
            SelectedSubtitleTrackIndex = selectedSubtitle?.Index,
            IsSubtitleBurnIn = subtitleBurnInStreamIndex.HasValue
        };

        return (new IndexedFileStreamUri
        {
            Uri = new Uri(GetHlsStreamManifestQueryUriBuilder.Build(new GetHlsStreamManifestQuery()
            {
                Id = indexedFile.Id,
                StreamSessionId = request.StreamSessionId,
                TranscodingVideoCodec = videoTranscodingMediaFormat?.VideoCodec,
                AudioTrackTranscodings = audioTrackTranscodings,
                DefaultAudioTrackIndex = request.AudioTrackIndex,
                DefaultSubtitleTrackIndex = defaultTextSubtitleTrackIndex,
                SubtitleBurnInStreamIndex = subtitleBurnInStreamIndex
            }), UriKind.Relative),
            MimeType = "application/vnd.apple.mpegurl"
        }, hlsDecision);
    }

    private static TranscodeReason BuildVideoTranscodeReason(
        bool requiresVideoTranscoding,
        bool forcedByMissingSegments,
        bool videoCodecSupported,
        bool audioNeedsTranscode,
        bool subtitlesBurnIn,
        bool resolutionExceedsDevice)
    {
        var reason = TranscodeReason.None;

        if (requiresVideoTranscoding)
        {
            if (subtitlesBurnIn)
                reason |= TranscodeReason.SubtitlesBurnIn;
            else if (forcedByMissingSegments)
                reason |= TranscodeReason.HlsSegmentsUnavailable;
            else if (resolutionExceedsDevice)
                reason |= TranscodeReason.ResolutionNotSupported;
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

    // Codecs that work inside fMP4 segments (HLS with ISO BMFF), ordered by transcoding preference.
    private static readonly string[] HlsFmp4VideoCodecPriority = ["h264", "hevc", "vp9", "av1", "mpeg4", "mpeg2"];

    public static VideoMediaFormat GetDeviceBestSupportedVideoMediaFormat(ICollection<BaseMediaFormat> supportedVideoCodecs)
    {
        var videoFormats = supportedVideoCodecs.OfType<VideoMediaFormat>().ToList();

        foreach (var codec in HlsFmp4VideoCodecPriority)
        {
            var mp4Match = videoFormats.FirstOrDefault(f =>
                f.Container == "mp4" && string.Equals(f.VideoCodec, codec, StringComparison.OrdinalIgnoreCase));
            if (mp4Match is not null)
                return mp4Match;
        }

        foreach (var codec in HlsFmp4VideoCodecPriority)
        {
            var match = videoFormats.FirstOrDefault(f =>
                string.Equals(f.VideoCodec, codec, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return videoFormats.First();
    }

    public static (IndexedFileStreamUri Uri, StreamDecisionDto Decision) GetAudioFileStreamUri(Device device, IndexedFile indexedFile, AudioFileMetadata audioFileMetadata, GetStreamUriQuery request)
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
                SelectedAudioTrackIndex = audioTrack.Index,
                AudioTrackLanguage = audioTrack.Language,
                AudioTrackTitle = audioTrack.Name,
                AudioChannelLayout = audioTrack.ChannelLayout
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
            SelectedAudioTrackIndex = audioTrack.Index,
            AudioTrackLanguage = audioTrack.Language,
            AudioTrackTitle = audioTrack.Name,
            AudioChannelLayout = audioTrack.ChannelLayout
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
