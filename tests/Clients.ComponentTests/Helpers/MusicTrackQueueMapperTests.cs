using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class MusicTrackQueueMapperTests
{
    private IK7ServerService _api = null!;

    [SetUp]
    public void SetUp()
    {
        _api = Substitute.For<IK7ServerService>();
        _api.GetAbsoluteUri(Arg.Any<string?>())
            .Returns(ci =>
            {
                var path = ci.Arg<string?>();
                return path is null ? null : new Uri("https://k7.local" + path);
            });
    }

    [Test]
    public void ToQueueItem_ShouldReturnNull_WhenIndexedFileMissing()
    {
        var track = new LiteMusicTrackDto
        {
            Id = Guid.NewGuid(),
            Title = "Song",
            IndexedFileId = null
        };

        MusicTrackQueueMapper.ToQueueItem(track, _api).Should().BeNull();
    }

    [Test]
    public void ToQueueItem_ShouldMapLiteTrackFieldsAndPreferCover()
    {
        var mediaId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var pictureId = Guid.NewGuid();
        var track = new LiteMusicTrackDto
        {
            Id = mediaId,
            Title = null,
            ArtistName = "Artist",
            ArtistId = Guid.NewGuid(),
            AlbumTitle = "Album",
            Genre = "Rock",
            IndexedFileId = fileId,
            Duration = 210,
            LoudnessLufs = -14,
            Pictures =
            [
                new MetadataPictureDto
                {
                    Id = Guid.NewGuid(),
                    Type = MetadataPictureType.Poster,
                    Uri = new Uri("/api/pictures/poster", UriKind.Relative)
                },
                new MetadataPictureDto
                {
                    Id = pictureId,
                    Type = MetadataPictureType.Cover,
                    DominantColor = "#112233",
                    Uri = new Uri("/api/pictures/cover", UriKind.Relative)
                }
            ]
        };

        var item = MusicTrackQueueMapper.ToQueueItem(track, _api, untitledLabel: "Sans titre");

        item.Should().NotBeNull();
        item!.MediaId.Should().Be(mediaId);
        item.IndexedFileId.Should().Be(fileId);
        item.Title.Should().Be("Sans titre");
        item.Artist.Should().Be("Artist");
        item.AlbumTitle.Should().Be("Album");
        item.Genre.Should().Be("Rock");
        item.Duration.Should().Be(210);
        item.LoudnessLufs.Should().Be(-14);
        item.CoverDominantColor.Should().Be("#112233");
        item.CoverUrl.Should().Contain("/api/pictures/cover");
    }

    [Test]
    public void ToQueueItems_ShouldSkipTracksWithoutIndexedFile()
    {
        var ok = new LiteMusicTrackDto
        {
            Id = Guid.NewGuid(),
            Title = "Ok",
            IndexedFileId = Guid.NewGuid(),
            Duration = 10
        };
        var skip = new LiteMusicTrackDto
        {
            Id = Guid.NewGuid(),
            Title = "Skip",
            IndexedFileId = null
        };

        var items = MusicTrackQueueMapper.ToQueueItems([ok, skip], _api);

        items.Should().ContainSingle();
        items[0].Title.Should().Be("Ok");
    }
}
