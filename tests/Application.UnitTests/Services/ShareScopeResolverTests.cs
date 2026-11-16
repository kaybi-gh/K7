using K7.Server.Application.Services;
using K7.Server.Application.UnitTests.Features.MediaShares;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

[TestFixture]
public class ShareScopeResolverTests
{
    private MediaShareTestDatabase _database = null!;
    private ShareScopeResolver _resolver = null!;

    [SetUp]
    public void SetUp()
    {
        _database = new MediaShareTestDatabase();
        _resolver = new ShareScopeResolver(_database.Context);
    }

    [TearDown]
    public void TearDown() => _database.Dispose();

    [Test]
    public async Task IsInScopeAsync_ShouldReturnTrue_WhenMediaIsTheRoot()
    {
        var inScope = await _resolver.IsInScopeAsync(_database.MovieId, _database.MovieId);

        inScope.Should().BeTrue();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldReturnFalse_WhenRootDoesNotExist()
    {
        var missing = Guid.NewGuid();

        var inScope = await _resolver.IsInScopeAsync(missing, missing);

        inScope.Should().BeFalse();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldCoverSeasonsAndEpisodes_WhenRootIsASerie()
    {
        (await _resolver.IsInScopeAsync(_database.SerieId, _database.Season1Id)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.SerieId, _database.Episode1Id)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.SerieId, _database.OtherSeasonEpisodeId)).Should().BeTrue();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldOnlyCoverItsEpisodes_WhenRootIsASeason()
    {
        (await _resolver.IsInScopeAsync(_database.Season1Id, _database.Episode1Id)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.Season1Id, _database.Episode2Id)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.Season1Id, _database.OtherSeasonEpisodeId)).Should().BeFalse();
        (await _resolver.IsInScopeAsync(_database.Season1Id, _database.Season2Id)).Should().BeFalse();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldNotCoverParentsOrSiblings_WhenRootIsAnEpisode()
    {
        (await _resolver.IsInScopeAsync(_database.Episode1Id, _database.Episode1Id)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.Episode1Id, _database.Episode2Id)).Should().BeFalse();
        (await _resolver.IsInScopeAsync(_database.Episode1Id, _database.Season1Id)).Should().BeFalse();
        (await _resolver.IsInScopeAsync(_database.Episode1Id, _database.SerieId)).Should().BeFalse();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldCoverAlbumsAndTracks_WhenRootIsAnArtist()
    {
        (await _resolver.IsInScopeAsync(_database.ArtistId, _database.AlbumId)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.ArtistId, _database.TrackId)).Should().BeTrue();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldNotCoverTracksOnOtherArtistsAlbums_WhenRootIsAnArtist()
    {
        (await _resolver.IsInScopeAsync(_database.ArtistId, _database.GuestTrackId)).Should().BeFalse();
        (await _resolver.IsInScopeAsync(_database.ArtistId, _database.OtherArtistAlbumId)).Should().BeFalse();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldNotCoverTheArtist_WhenRootIsAnAlbum()
    {
        (await _resolver.IsInScopeAsync(_database.AlbumId, _database.TrackId)).Should().BeTrue();
        (await _resolver.IsInScopeAsync(_database.AlbumId, _database.ArtistId)).Should().BeFalse();
    }

    [Test]
    public async Task IsInScopeAsync_ShouldReturnFalse_WhenMediaIsUnrelated()
    {
        var inScope = await _resolver.IsInScopeAsync(_database.SerieId, _database.MovieId);

        inScope.Should().BeFalse();
    }

    [TestCase(MediaType.Movie, true)]
    [TestCase(MediaType.SerieEpisode, true)]
    [TestCase(MediaType.MusicTrack, true)]
    [TestCase(MediaType.Serie, false)]
    [TestCase(MediaType.SerieSeason, false)]
    [TestCase(MediaType.MusicAlbum, false)]
    [TestCase(MediaType.MusicArtist, false)]
    public void IsSinglePlayableItem_ShouldOnlyAcceptLeafTypes(MediaType type, bool expected)
    {
        ShareScopeResolver.IsSinglePlayableItem(type).Should().Be(expected);
    }
}
