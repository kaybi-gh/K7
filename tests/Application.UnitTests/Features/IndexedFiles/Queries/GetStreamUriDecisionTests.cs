using K7.Server.Application.Features.IndexedFiles.Queries.GetStreamUri;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

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
    public void GetAudioFileStreamUri_ShouldReturnDirect_WhenWebClient()
    {
        var device = CreateDevice(["audio-mp3-mp3"], ClientType.Web, OperatingSystem.Unknown);
        var (indexedFile, metadata) = CreateAudioFile("mp3", "mp3");
        var request = new GetStreamUriQuery { Id = indexedFile.Id, StreamSessionId = Guid.NewGuid() };

        var (_, decision) = GetStreamUriQueryHandler.GetAudioFileStreamUri(device, indexedFile, metadata, request);

        decision.Mode.Should().Be(PlaybackMode.Direct);
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
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenMatroskaHevcAacSupported()
    {
        var device = CreateDevice(["audio-matroska-aac", "video-matroska-aac-hevc"]);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "hevc", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
        uri.MimeType.Should().Be("video/x-matroska");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenFfprobeCodecAliasesMatchCatalog()
    {
        var device = CreateDevice(["audio-matroska-pcm", "video-matroska-mpeg2"]);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "mpeg2video", "pcm_s16le");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
        uri.MimeType.Should().Be("video/x-matroska");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenM4vHevcAacSupported()
    {
        var device = CreateDevice(["audio-m4v-aac", "video-m4v-hevc"]);
        var (indexedFile, metadata) = CreateVideoFile("m4v", "hevc", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenIosNativeAv1()
    {
        var device = CreateDevice(
            ["audio-matroska-opus", "video-matroska-av1"],
            ClientType.Native,
            OperatingSystem.iOS);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "av01", "opus");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnHlsRemux_WhenWebClientEvenIfCodecsSupported()
    {
        var device = CreateDevice(
            ["audio-mp4-aac", "video-mp4-aac-h264"],
            ClientType.Web,
            OperatingSystem.Unknown);
        var (indexedFile, metadata) = CreateVideoFile("mp4", "h264", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (uri, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Transmux);
        uri.MimeType.Should().Be("application/vnd.apple.mpegurl");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnHlsRemux_WhenWindowsVideoJs()
    {
        var device = CreateDevice(
            ["audio-mp4-aac", "video-mp4-aac-h264"],
            ClientType.Native,
            OperatingSystem.Windows);
        var (indexedFile, metadata) = CreateVideoFile("mp4", "h264", "aac");
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Transmux);
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldTranscodeHevcMain10_WhenWebClient()
    {
        var device = CreateDevice(
            ["audio-mp4-aac", "video-mp4-aac-h264", "video-mp4-aac-hevc"],
            ClientType.Web,
            OperatingSystem.Unknown);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "hevc", "aac");
        var main10Track = metadata.VideoTracks.First();
        main10Track.Profile = "Main 10";
        main10Track.BitDepth = 10;
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Transcode);
        decision.StreamVideoCodec.Should().Be("h264");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldRemuxHevc8Bit_WhenWebClient()
    {
        var device = CreateDevice(
            ["audio-mp4-aac", "video-mp4-aac-h264", "video-mp4-aac-hevc"],
            ClientType.Web,
            OperatingSystem.Unknown);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "hevc", "aac");
        var videoTrack = metadata.VideoTracks.First();
        videoTrack.Profile = "Main";
        videoTrack.BitDepth = 8;
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Transmux);
        decision.StreamVideoCodec.Should().Be("hevc");
    }

    [Test]
    public void GetVideoFileStreamUri_ShouldReturnDirect_WhenNativeHevcMain10()
    {
        var device = CreateDevice(["audio-matroska-aac", "video-matroska-aac-hevc"]);
        var (indexedFile, metadata) = CreateVideoFile("matroska", "hevc", "aac");
        var nativeHevcTrack = metadata.VideoTracks.First();
        nativeHevcTrack.Profile = "Main 10";
        nativeHevcTrack.BitDepth = 10;
        var request = new GetStreamUriQuery
        {
            Id = indexedFile.Id,
            StreamSessionId = Guid.NewGuid(),
            AudioTrackIndex = 0
        };

        var (_, decision) = GetStreamUriQueryHandler.GetVideoFileStreamUri(
            device, indexedFile, metadata, request, hlsSegmentsAvailable: true, subtitleTrackIndex: null);

        decision.Mode.Should().Be(PlaybackMode.Direct);
    }

    [TestCase(ClientType.Native, OperatingSystem.Android, true)]
    [TestCase(ClientType.Native, OperatingSystem.iOS, true)]
    [TestCase(ClientType.Native, OperatingSystem.MacCatalyst, true)]
    [TestCase(ClientType.Native, OperatingSystem.Windows, false)]
    [TestCase(ClientType.Web, OperatingSystem.Unknown, false)]
    public void AllowsVideoDirectPlay_ShouldMatchVideoJsLimitation(
        ClientType clientType,
        OperatingSystem operatingSystem,
        bool expected)
    {
        var device = CreateDevice(["video-mp4-aac-h264"], clientType, operatingSystem);
        GetStreamUriQueryHandler.AllowsVideoDirectPlay(device).Should().Be(expected);
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

    private static Device CreateDevice(
        IEnumerable<string> formatIds,
        ClientType clientType = ClientType.Native,
        OperatingSystem operatingSystem = OperatingSystem.Android) => new()
    {
        ClientType = clientType,
        OperatingSystem = operatingSystem,
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
