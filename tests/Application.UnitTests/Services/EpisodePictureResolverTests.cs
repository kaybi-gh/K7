using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Services;

public class EpisodePictureResolverTests
{
    private static readonly Guid SerieId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public void ResolveHeroPictures_ShouldPreferEpisodeStill_WhenAvailable()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var still = CreatePicture(MetadataPictureType.Still);
        var episode = CreateEpisode();
        episode.Serie!.Pictures.Add(seriePoster);
        episode.Pictures.Add(still);

        var result = EpisodePictureResolver.ResolveHeroPictures(episode);

        result.Should().ContainSingle().Which.Should().BeSameAs(still);
    }

    [Test]
    public void ResolveDisplayPictures_ShouldPreferSeasonPoster_WhenSeasonAndSerieBothHavePosters()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var seasonPoster = CreatePicture(MetadataPictureType.Poster);
        var episode = CreateEpisode();
        episode.Serie!.Pictures.Add(seriePoster);
        episode.Season!.Pictures.Add(seasonPoster);

        var result = EpisodePictureResolver.ResolveDisplayPictures(episode);

        result.Should().HaveCount(2);
        result![0].Should().BeSameAs(seasonPoster);
        result[1].Should().BeSameAs(seriePoster);
    }

    [Test]
    public void ResolveDisplayPictures_ShouldPreferSeriePoster_WhenSeasonHasNoPictures()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var episode = CreateEpisode();
        episode.Serie!.Pictures.Add(seriePoster);
        episode.Pictures.Add(CreatePicture(MetadataPictureType.Still));

        var result = EpisodePictureResolver.ResolveDisplayPictures(episode);

        result.Should().ContainInOrder(seriePoster);
        result.Should().ContainSingle(p => p.Type == MetadataPictureType.Still);
    }

    [Test]
    public void ResolveDisplayPictures_ShouldFallBackToEpisodeStill_WhenSerieHasNoPoster()
    {
        var still = CreatePicture(MetadataPictureType.Still);
        var episode = CreateEpisode();
        episode.Pictures.Add(still);

        var result = EpisodePictureResolver.ResolveDisplayPictures(episode);

        result.Should().ContainSingle().Which.Should().BeSameAs(still);
    }

    [Test]
    public void ResolveDisplayPictures_ShouldFallBackToSeasonPoster_WhenSerieIsEmpty()
    {
        var seasonPoster = CreatePicture(MetadataPictureType.Poster);
        var episode = CreateEpisode();
        episode.Season!.Pictures.Add(seasonPoster);

        var result = EpisodePictureResolver.ResolveDisplayPictures(episode);

        result.Should().ContainSingle().Which.Should().BeSameAs(seasonPoster);
    }

    [Test]
    public void ResolveDisplayPictures_ShouldReturnNull_WhenNoPicturesExist()
    {
        var episode = CreateEpisode();

        EpisodePictureResolver.ResolveDisplayPictures(episode).Should().BeNull();
    }

    [Test]
    public void MergeHeroAndDisplayPictures_ShouldPutSeasonPosterBeforeStill_WhenEpisodeHasStill()
    {
        var seriePoster = CreatePicture(MetadataPictureType.Poster);
        var seasonPoster = CreatePicture(MetadataPictureType.Poster);
        var still = CreatePicture(MetadataPictureType.Still);
        var episode = CreateEpisode();
        episode.Serie!.Pictures.Add(seriePoster);
        episode.Season!.Pictures.Add(seasonPoster);
        episode.Pictures.Add(still);

        var result = EpisodePictureResolver.MergeHeroAndDisplayPictures(episode);

        result.Should().NotBeNull();
        var pictures = result!;
        pictures.Should().Contain(still);
        pictures.Should().Contain(seasonPoster);
        pictures.Should().Contain(seriePoster);
        pictures[0].Should().BeSameAs(seasonPoster);
        pictures.Should().Contain(p => p.Type == MetadataPictureType.Still);
    }

    private static SerieEpisode CreateEpisode()
    {
        var serie = new Serie { Id = SerieId, Title = "Color Classics" };
        var season = new SerieSeason { SeasonNumber = 1, SerieId = SerieId, Serie = serie };
        return new SerieEpisode
        {
            SerieId = SerieId,
            Serie = serie,
            Season = season,
            EpisodeNumber = 2,
        };
    }

    private static MetadataPicture CreatePicture(MetadataPictureType type) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
    };
}
