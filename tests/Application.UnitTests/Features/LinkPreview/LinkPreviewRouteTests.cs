using K7.Server.Application.Features.LinkPreview.Queries.GetLinkPreview;

namespace K7.Server.Application.UnitTests.Features.LinkPreview;

[TestFixture]
public class LinkPreviewRouteTests
{
    [Test]
    public void TryParse_ShouldReadMovie()
    {
        var id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        LinkPreviewRoute.TryParse($"/movies/{id}", out var target).Should().BeTrue();
        target.Kind.Should().Be(LinkPreviewKind.Movie);
        target.Id.Should().Be(id);
    }

    [Test]
    public void TryParse_ShouldReadSerieSeasonAndEpisode()
    {
        var id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        LinkPreviewRoute.TryParse($"/series/{id}/seasons/2", out var season).Should().BeTrue();
        season.Kind.Should().Be(LinkPreviewKind.Season);
        season.Id.Should().Be(id);
        season.SeasonNumber.Should().Be(2);

        LinkPreviewRoute.TryParse($"/series/{id}/seasons/2/episodes/7/", out var episode).Should().BeTrue();
        episode.Kind.Should().Be(LinkPreviewKind.Episode);
        episode.EpisodeNumber.Should().Be(7);

        LinkPreviewRoute.TryParse(
                "/series/019fc962-182a-7399-ae88-34e8a26ab7e3/seasons/1/episodes/1",
                out var uuidv7)
            .Should().BeTrue();
        uuidv7.Kind.Should().Be(LinkPreviewKind.Episode);
        uuidv7.SeasonNumber.Should().Be(1);
        uuidv7.EpisodeNumber.Should().Be(1);
    }

    [Test]
    public void TryParse_ShouldReadMusicPages()
    {
        var id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        LinkPreviewRoute.TryParse($"/music/albums/{id}", out var album).Should().BeTrue();
        album.Kind.Should().Be(LinkPreviewKind.Album);

        LinkPreviewRoute.TryParse($"/MUSIC/artists/{id}", out var artist).Should().BeTrue();
        artist.Kind.Should().Be(LinkPreviewKind.Artist);
    }

    [Test]
    public void TryParse_ShouldRejectUnknownPaths()
    {
        LinkPreviewRoute.TryParse("/", out _).Should().BeFalse();
        LinkPreviewRoute.TryParse("/home", out _).Should().BeFalse();
        LinkPreviewRoute.TryParse("/playlists/" + Guid.NewGuid(), out _).Should().BeFalse();
        LinkPreviewRoute.TryParse("/persons/" + Guid.NewGuid(), out _).Should().BeFalse();
        LinkPreviewRoute.TryParse("/movies/not-a-guid", out _).Should().BeFalse();
    }
}
