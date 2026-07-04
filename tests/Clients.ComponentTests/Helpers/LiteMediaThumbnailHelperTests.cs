using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class LiteMediaThumbnailHelperTests
{
    [Test]
    public void ResolvePicture_ShouldFallBackToSeriePoster_WhenSeasonHasNoPoster()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var season = new LiteSerieSeasonDto
        {
            Id = Guid.NewGuid(),
            Title = "Season 1",
            SerieId = Guid.NewGuid(),
            SeasonNumber = 1,
            EpisodeCount = 10,
            SeriePictures = [seriePoster]
        };

        var result = LiteMediaPictureResolver.ResolvePicture(season);

        result.Should().BeSameAs(seriePoster);
    }

    [Test]
    public void ResolvePicture_ShouldFallBackToSeriePoster_WhenEpisodeHasNoStill()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var episode = new LiteSerieEpisodeDto
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            SerieId = Guid.NewGuid(),
            SeasonNumber = 1,
            EpisodeNumber = 1,
            SerieSeasonCount = 1,
            SeriePictures = [seriePoster]
        };

        var result = LiteMediaPictureResolver.ResolvePicture(episode);

        result.Should().BeSameAs(seriePoster);
    }

    [Test]
    public void ResolveEpisodeStill_ShouldReturnNull_WhenOnlyPosterExists()
    {
        var episode = new LiteSerieEpisodeDto
        {
            Id = Guid.NewGuid(),
            Title = "Pilot",
            SerieId = Guid.NewGuid(),
            SeasonNumber = 1,
            EpisodeNumber = 1,
            SerieSeasonCount = 1,
            Pictures = [CreatePicture(MetadataPictureType.Poster)],
            SeriePictures = [CreatePicture(MetadataPictureType.Poster)]
        };

        LiteMediaPictureResolver.ResolveEpisodeStill(episode).Should().BeNull();
    }

    [Test]
    public void GetThumbShape_ShouldUsePoster_ForMovies()
    {
        var movie = new LiteMovieDto { Id = Guid.NewGuid(), Title = "Inception" };

        LiteMediaThumbnailHelper.GetThumbShape(movie).Should().Be(LiteMediaThumbnailHelper.ThumbShape.Poster);
    }

    private static MetadataPictureDto CreatePicture(MetadataPictureType type) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Uri = new Uri("/api/pictures/test.jpg", UriKind.Relative)
    };
}
