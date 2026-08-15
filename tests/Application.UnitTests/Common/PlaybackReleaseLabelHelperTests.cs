using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Application.UnitTests.Common;

public class PlaybackReleaseLabelHelperTests
{
    [Test]
    public void Format_ShouldPreferTechnicalSpecs_OverFileName()
    {
        var file = File(
            "Dune",
            VideoResolutionIdentifier._1080p,
            [
                Audio(0, "fr", "vff"),
                Audio(1, "en", "eng")
            ],
            "hevc",
            12L * 1024 * 1024 * 1024);

        var label = PlaybackReleaseLabelHelper.Format(file, sourceLabel: "Local");

        label.Should().Contain("1080p");
        label.Should().Contain("VFF");
        label.Should().Contain("EN");
        label.Should().Contain("hevc");
        label.Should().Contain("GB");
        label.Should().Contain("Local");
        label.Should().NotContain("Dune");
    }

    [Test]
    public void Format_ShouldKeepTwoFrenchDubsDistinct()
    {
        var file = File(
            "Movie",
            VideoResolutionIdentifier._2160p,
            [Audio(0, "fr", "France"), Audio(1, "fr", "Canadien")],
            "hevc",
            40L * 1024 * 1024 * 1024);

        var label = PlaybackReleaseLabelHelper.Format(file);

        label.Should().Contain("4K");
        label.Should().Contain("France");
        label.Should().Contain("Canadien");
    }

    [Test]
    public void Format_ShouldUseRemoteSummary_WhenDetailsMissing()
    {
        var remote = new RemoteIndexedFileDto
        {
            Id = Guid.NewGuid(),
            PeerServerId = Guid.NewGuid(),
            RemoteFileId = Guid.NewGuid(),
            RemoteMediaId = Guid.NewGuid(),
            Name = "Dune",
            Extension = ".mkv",
            Size = 8L * 1024 * 1024 * 1024,
            VideoResolution = VideoResolutionIdentifier._720p
        };

        var label = PlaybackReleaseLabelHelper.Format(null, remote, "Federation");

        label.Should().Contain("720p");
        label.Should().Contain("GB");
        label.Should().Contain("Federation");
        label.Should().NotContain("Dune");
    }

    [Test]
    public void Format_ShouldFallBackToName_WhenNoTechnicalMetadata()
    {
        var file = new IndexedFileDto
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Name = "orphan-release",
            Extension = ".mkv",
            Path = @"D:\orphan-release.mkv",
            Hash = 1,
            Size = 0
        };

        PlaybackReleaseLabelHelper.Format(file).Should().Be("orphan-release");
    }

    private static IndexedFileDto File(
        string name,
        VideoResolutionIdentifier resolution,
        IReadOnlyList<AudioFileTrackDto> audio,
        string codec,
        long size) => new()
    {
        Id = Guid.NewGuid(),
        LibraryId = Guid.NewGuid(),
        Name = name,
        Extension = ".mkv",
        Path = $@"D:\{name}.mkv",
        Hash = 1,
        Size = size,
        FileMetadata = new VideoFileMetadataDto
        {
            Container = "mkv",
            VideoBitrate = 5_000_000,
            VideoResolution = resolution,
            AudioTracks = audio,
            VideoTracks =
            [
                new VideoFileTrackDto
                {
                    Index = 0,
                    Width = 1920,
                    Height = 1080,
                    Codec = codec,
                    Profile = "main",
                    Level = 150
                }
            ]
        }
    };

    private static AudioFileTrackDto Audio(int index, string language, string name) => new()
    {
        Index = index,
        Language = language,
        Name = name,
        Codec = "ac3",
        Channels = 6
    };
}
