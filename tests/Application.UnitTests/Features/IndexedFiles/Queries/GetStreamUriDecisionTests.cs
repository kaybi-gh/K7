using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Queries;

public class GetStreamUriDecisionTests
{
    [Test]
    public void GetAudioFileStreamUri_ShouldReturnDirect_WhenContainerAndCodecSupported()
    {
        var device = CreateDevice(["audio-mp3-mp3"]);
        var (indexedFile, metadata) = CreateAudioFile("mp3", "mp3");
        var request = new GetStreamUriQuery { Id = indexedFile.Id, StreamSessionId = Guid.NewGuid() };

        var (uri, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(device, indexedFile, metadata, request);

        decision.Mode.Should().Be(PlaybackMode.Direct);
        uri.MimeType.Should().Be("audio/mpeg");
        uri.Uri.ToString().Should().Contain(indexedFile.Id.ToString());
    }

    [Test]
    public void GetAudioFileStreamUri_ShouldReturnHlsTranscode_WhenCodecUnsupported()
    {
        var device = CreateDevice(["audio-mp4-aac"]);
        var (indexedFile, metadata) = CreateAudioFile("flac", "flac");
        var request = new GetStreamUriQuery { Id = indexedFile.Id, StreamSessionId = Guid.NewGuid() };

        var (uri, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(device, indexedFile, metadata, request);

        decision.Mode.Should().Be(PlaybackMode.Transcode);
        decision.Reason.Should().HaveFlag(TranscodeReason.AudioCodecNotSupported);
        decision.StreamAudioCodec.Should().Be("aac");
        uri.MimeType.Should().Be("application/vnd.apple.mpegurl");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenAudioAndVideoSupported()
    {
        var device = CreateDevice(["audio-mp4-aac", "video-mp4-aac-h264"]);
        var (indexedFile, metadata) = CreateVideoFile("mp4", "h264", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
        uri.MimeType.Should().Be("video/mp4");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldForceTranscode_WhenHlsSegmentsMissing()
    {
        var device = CreateDevice(["audio-mp4-aac", "video-mp4-aac-h264"]);
        // Container mismatch => not direct, but h264 is supported so segments missing should force transcoding
        var (indexedFile, metadata) = CreateVideoFile("matroska", "h264", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: false, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Transcode);
        decision.Reason.Should().HaveFlag(TranscodeReason.HlsSegmentsUnavailable);
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldForceBurnIn_WhenImageSubtitleSelectedOnHlsPath()
    {
        var device = CreateDevice(["audio-mp4-aac", "video-mp4-aac-h264"]);
        // Non-direct container forces HLS before burn-in is applied.
        var (indexedFile, metadata) = CreateVideoFile("matroska", "h264", "aac");
        metadata.SubtitleTracks.Add(new SubtitleFileTrack
        {
            Index = 1,
            Codec = "hdmv_pgs_subtitle",
            IsTextBased = false
        });
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: 1);

        decision.Mode.Should().Be(PlaybackMode.Transcode);
        decision.IsSubtitleBurnIn.Should().BeTrue();
        decision.Reason.Should().HaveFlag(TranscodeReason.SubtitlesBurnIn);
    }

    [Test]
    public void GetDeviceBestSupportedAudioMediaFormat_ShouldPreferAacOverOpus()
    {
        var device = CreateDevice(["audio-webm-opus", "audio-mp4-aac"]);
        var formats = device.PlaybackCapabilities.SupportedMediaFormats.ToList();

        var best = GetStreamUriQueryHandler.GetDeviceBestSupportedAudioMediaFormat(formats);

        best.Codec.Should().Be("aac");
    }

    private static Device CreateDevice(IEnumerable<string> formatIds) => new()
    {
        ClientType = ClientType.Web,
        PlaybackCapabilities = new DevicePlaybackCapabilities
        {
            SupportedMediaFormatIds = formatIds.ToList()
        }
    };

    private static (IndexedFile File, AudioFileMetadata Metadata) CreateAudioFile(string container, string codec)
    {
        var id = Guid.NewGuid();
        var metadata = new AudioFileMetadata
        {
            Container = container,
            Duration = TimeSpan.FromMinutes(3),
            AudioTrack = new AudioFileTrack
            {
                Index = 0,
                Codec = codec,
                Channels = 2
            }
        };
        var file = new IndexedFile
        {
            Id = id,
            LibraryId = Guid.NewGuid(),
            Name = "track",
            Extension = $".{container}",
            Path = $"/media/track.{container}",
            Hash = 1,
            Size = 1,
            FileMetadata = metadata
        };
        return (file, metadata);
    }

    private static (IndexedFile File, VideoFileMetadata Metadata) CreateVideoFile(
        string container,
        string videoCodec,
        string audioCodec)
    {
        var id = Guid.NewGuid();
        var metadata = new VideoFileMetadata
        {
            Container = container,
            VideoBitrate = 5_000_000,
            VideoResolution = VideoResolutionIdentifier._1080p,
            Duration = TimeSpan.FromHours(2),
            AudioTracks =
            [
                new AudioFileTrack
                {
                    Index = 0,
                    Codec = audioCodec,
                    Channels = 2,
                    IsDefault = true
                }
            ],
            VideoTracks =
            [
                new VideoFileTrack
                {
                    Index = 0,
                    Codec = videoCodec,
                    Width = 1920,
                    Height = 1080,
                    Profile = "high",
                    Level = 40
                }
            ]
        };
        var file = new IndexedFile
        {
            Id = id,
            LibraryId = Guid.NewGuid(),
            Name = "movie",
            Extension = $".{container}",
            Path = $"/media/movie.{container}",
            Hash = 1,
            Size = 1,
            FileMetadata = metadata
        };
        return (file, metadata);
    }
}
