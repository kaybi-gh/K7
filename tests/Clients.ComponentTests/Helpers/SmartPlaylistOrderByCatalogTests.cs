using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SmartPlaylistOrderByCatalogTests
{
    [Test]
    public void GetOptions_MusicTrack_ShouldIncludeMusicSortFields()
    {
        var options = SmartPlaylistOrderByCatalog.GetOptions(MediaType.MusicTrack);

        options.Should().Contain(SmartPlaylistOrderBy.ArtistName);
        options.Should().Contain(SmartPlaylistOrderBy.AlbumTitle);
        options.Should().Contain(SmartPlaylistOrderBy.DateAdded);
    }

    [Test]
    public void GetOptions_Movie_ShouldExcludeMusicSortFields()
    {
        var options = SmartPlaylistOrderByCatalog.GetOptions(MediaType.Movie);

        options.Should().NotContain(SmartPlaylistOrderBy.ArtistName);
        options.Should().NotContain(SmartPlaylistOrderBy.AlbumTitle);
        options.Should().Contain(SmartPlaylistOrderBy.Title);
    }

    [Test]
    public void Normalize_MovieWithArtistSort_ShouldFallbackToDateAdded()
    {
        SmartPlaylistOrderByCatalog.Normalize(SmartPlaylistOrderBy.ArtistName, MediaType.Movie)
            .Should()
            .Be(SmartPlaylistOrderBy.DateAdded);
    }
}
