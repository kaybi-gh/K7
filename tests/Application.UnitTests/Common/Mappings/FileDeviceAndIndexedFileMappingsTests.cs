using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Server.Application.UnitTests.Common.Mappings;

[TestFixture]
public class FileDeviceAndIndexedFileMappingsTests
{
    [Test]
    public void ToFileMetadataDto_ShouldMapAudioAndVideoTracks()
    {
        var audio = new AudioFileMetadata
        {
            Id = Guid.NewGuid(),
            Container = "mp3",
            Duration = TimeSpan.FromMinutes(3),
            AudioTrack = new AudioFileTrack
            {
                Index = 0,
                Codec = "mp3",
                Channels = 2,
                Language = "eng"
            }
        };

        var video = new VideoFileMetadata
        {
            Id = Guid.NewGuid(),
            Container = "mp4",
            Duration = TimeSpan.FromHours(2),
            VideoBitrate = 5_000_000,
            VideoResolution = VideoResolutionIdentifier._1080p,
            AudioTracks =
            [
                new AudioFileTrack { Index = 0, Codec = "aac", Channels = 2, IsDefault = true }
            ],
            VideoTracks =
            [
                new VideoFileTrack
                {
                    Index = 0,
                    Codec = "h264",
                    Width = 1920,
                    Height = 1080,
                    Profile = "high",
                    Level = 40
                }
            ],
            SubtitleTracks =
            [
                new SubtitleFileTrack { Index = 1, Codec = "subrip", Language = "fra", IsTextBased = true }
            ]
        };

        var audioDto = audio.ToFileMetadataDto().Should().BeOfType<AudioFileMetadataDto>().Subject;
        audioDto.Container.Should().Be("mp3");
        audioDto.AudioTrack!.Codec.Should().Be("mp3");

        var videoDto = video.ToFileMetadataDto().Should().BeOfType<VideoFileMetadataDto>().Subject;
        videoDto.VideoResolution.Should().Be(VideoResolutionIdentifier._1080p);
        videoDto.AudioTracks.Should().ContainSingle();
        videoDto.VideoTracks.Should().ContainSingle();
        videoDto.SubtitleTracks.Should().ContainSingle(t => t.Language == "fra");
    }

    [Test]
    public void ToIndexedFileDto_ShouldMapCoreFieldsAndMetadata()
    {
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = "movie",
            Extension = ".mkv",
            Path = "/media/movie.mkv",
            ParentDirectory = "/media",
            Hash = 42,
            Size = 1000,
            FileMetadata = new VideoFileMetadata
            {
                Id = Guid.NewGuid(),
                Container = "matroska",
                VideoBitrate = 1,
                VideoResolution = VideoResolutionIdentifier._720p,
                Duration = TimeSpan.FromMinutes(90)
            }
        };

        var dto = file.ToIndexedFileDto();

        dto.Id.Should().Be(file.Id);
        dto.Name.Should().Be("movie");
        dto.Path.Should().Be("/media/movie.mkv");
        dto.Hash.Should().Be(42);
        dto.Identification.Should().BeNull();
        dto.FileMetadata.Should().BeOfType<VideoFileMetadataDto>();
    }

    [Test]
    public void ToIndexedFileDto_ShouldMapIdentification()
    {
        var file = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = "Show.S01E01",
            Extension = ".mkv",
            Path = "/media/Show.S01E01.mkv",
            Hash = 1,
            Size = 100,
            Identification = new MediaIdentification("Show S01E01")
            {
                SeriesTitle = "Show",
                SeasonNumber = 1,
                EpisodeNumber = 1,
                ReleaseYear = new DateOnly(2020, 1, 1)
            }
        };

        var dto = file.ToIndexedFileDto();

        dto.Identification.Should().NotBeNull();
        dto.Identification!.Title.Should().Be("Show S01E01");
        dto.Identification.SeriesTitle.Should().Be("Show");
        dto.Identification.SeasonNumber.Should().Be(1);
        dto.Identification.EpisodeNumber.Should().Be(1);
        dto.Identification.ReleaseYear.Should().Be(new DateOnly(2020, 1, 1));
    }

    [Test]
    public void ToDevicePlaybackCapabilitiesDto_ShouldResolveKnownFormats()
    {
        var capabilities = new DevicePlaybackCapabilities
        {
            SupportedMediaFormatIds = ["audio-mp3-mp3", "video-mp4-aac-h264"],
            SupportedSubtitlesCodecs = ["subrip"],
            SupportsHDR = true
        };

        var dto = capabilities.ToDevicePlaybackCapabilitiesDto();

        dto.SupportsHDR.Should().BeTrue();
        dto.SupportedSubtitlesCodecs.Should().Equal("subrip");
        dto.SupportedMediaFormats.Should().HaveCount(2);
        dto.SupportedMediaFormats.Select(f => f.Id).Should().Contain(["audio-mp3-mp3", "video-mp4-aac-h264"]);
    }
}
