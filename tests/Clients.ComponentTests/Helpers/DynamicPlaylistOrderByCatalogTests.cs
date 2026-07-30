using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class DynamicPlaylistOrderByCatalogTests
{
    [Test]
    public void GetOptions_MusicTrack_ShouldIncludeMusicSortFields()
    {
        var options = DynamicPlaylistOrderByCatalog.GetOptions(MediaType.MusicTrack);

        options.Should().Contain(DynamicPlaylistOrderBy.ArtistName);
        options.Should().Contain(DynamicPlaylistOrderBy.AlbumTitle);
        options.Should().Contain(DynamicPlaylistOrderBy.DateAdded);
    }

    [Test]
    public void GetOptions_Movie_ShouldExcludeMusicSortFields()
    {
        var options = DynamicPlaylistOrderByCatalog.GetOptions(MediaType.Movie);

        options.Should().NotContain(DynamicPlaylistOrderBy.ArtistName);
        options.Should().NotContain(DynamicPlaylistOrderBy.AlbumTitle);
        options.Should().Contain(DynamicPlaylistOrderBy.Title);
    }

    [Test]
    public void Normalize_MovieWithArtistSort_ShouldFallbackToDateAdded()
    {
        DynamicPlaylistOrderByCatalog.Normalize(DynamicPlaylistOrderBy.ArtistName, MediaType.Movie)
            .Should()
            .Be(DynamicPlaylistOrderBy.DateAdded);
    }
}
