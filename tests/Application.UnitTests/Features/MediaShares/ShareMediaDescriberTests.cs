using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Features.MediaShares;

[TestFixture]
public class ShareMediaDescriberTests
{
    private MediaShareTestDatabase _database = null!;

    [SetUp]
    public void SetUp() => _database = new MediaShareTestDatabase();

    [TearDown]
    public void TearDown() => _database.Dispose();

    private Guid AddPicture(Guid mediaId, MetadataPictureType type, string? localPath = "/pictures/file.jpg")
    {
        var picture = new MetadataPicture { Id = Guid.NewGuid(), MediaId = mediaId, Type = type, LocalPath = localPath };
        _database.Context.MetadataPictures.Add(picture);
        _database.Context.SaveChanges();
        _database.Context.ChangeTracker.Clear();
        return picture.Id;
    }

    [Test]
    public async Task DescribeAsync_ShouldReturnNothing_WhenNoMediaIsAsked()
    {
        var descriptions = await ShareMediaDescriber.DescribeAsync(_database.Context, []);

        descriptions.Should().BeEmpty();
    }

    [Test]
    public async Task DescribeAsync_ShouldUseTitleAndPoster_WhenMediaIsAMovie()
    {
        AddPicture(_database.MovieId, MetadataPictureType.Backdrop);
        var posterId = AddPicture(_database.MovieId, MetadataPictureType.Poster);

        var descriptions = await ShareMediaDescriber.DescribeAsync(_database.Context, [_database.MovieId]);

        var description = descriptions[_database.MovieId];
        description.Type.Should().Be(MediaType.Movie);
        description.Title.Should().Be("Movie");
        description.Subtitle.Should().BeNull();
        description.PictureId.Should().Be(posterId);
        description.PictureUri.Should().Be($"/api/metadata-pictures/{posterId}");
    }

    [Test]
    public async Task DescribeAsync_ShouldFallBackToNumberingAndSerieArtwork_WhenEpisodeHasNoTitleOrPicture()
    {
        var seriePosterId = AddPicture(_database.SerieId, MetadataPictureType.Poster);

        var descriptions = await ShareMediaDescriber.DescribeAsync(_database.Context, [_database.Episode2Id]);

        var description = descriptions[_database.Episode2Id];
        description.Title.Should().Be("S01E02");
        description.Subtitle.Should().Be("Serie - S01E02");
        description.PictureId.Should().Be(seriePosterId);
    }

    [Test]
    public async Task DescribeAsync_ShouldGiveParentContext_WhenMediaIsASeasonAnAlbumOrATrack()
    {
        var descriptions = await ShareMediaDescriber.DescribeAsync(
            _database.Context,
            [_database.Season1Id, _database.AlbumId, _database.TrackId]);

        descriptions[_database.Season1Id].Title.Should().Be("S01");
        descriptions[_database.Season1Id].Subtitle.Should().Be("Serie");
        descriptions[_database.AlbumId].Subtitle.Should().Be("Artist");
        descriptions[_database.TrackId].Subtitle.Should().Be("Album");
    }

    [Test]
    public async Task DescribeAsync_ShouldIgnorePicturesNotDownloadedYet()
    {
        AddPicture(_database.MovieId, MetadataPictureType.Poster, localPath: null);

        var descriptions = await ShareMediaDescriber.DescribeAsync(_database.Context, [_database.MovieId]);

        descriptions[_database.MovieId].PictureId.Should().BeNull();
    }
}
