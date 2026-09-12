using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Features.Restrictions;

public class AgeRestrictionEvaluatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 10);

    [Test]
    public void Apply_ShouldHideMovieWithRatingAboveViewerAge()
    {
        var movies = CreateLibrary();
        var dob = new DateOnly(2015, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(movies.AsQueryable(), dob, Today).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Family", "Song");
    }

    [Test]
    public void Apply_ShouldHideUnratedMoviesAndSeries()
    {
        var movies = CreateLibrary();
        var dob = new DateOnly(2000, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(movies.AsQueryable(), dob, Today).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Family", "Teen", "Song");
    }

    [Test]
    public void Apply_ShouldKeepUnratedMoviesAndSeries_WhenHideUnratedIsOff()
    {
        var movies = CreateLibrary();
        var dob = new DateOnly(2000, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(movies.AsQueryable(), dob, Today, hideUnrated: false).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Family", "Teen", "Unrated", "Song");
    }

    [Test]
    public void Apply_ShouldHideUnknownRating_WhenHideUnratedIsOff()
    {
        var family = new Movie { Id = Guid.NewGuid(), Title = "Family" };
        family.MetadataTags.Add(CreateRatingTag(family, "g", "G"));
        var unknown = new Movie { Id = Guid.NewGuid(), Title = "Unknown" };
        unknown.MetadataTags.Add(CreateRatingTag(unknown, "xyz", "XYZ"));
        var medias = new List<BaseMedia> { family, unknown };

        var result = AgeRestrictionEvaluator.Apply(
            medias.AsQueryable(), new DateOnly(2000, 1, 1), Today, hideUnrated: false).ToList();

        result.Select(m => m.Title).Should().Equal("Family");
    }

    [Test]
    public void Apply_ShouldKeepMusicWithoutRating()
    {
        var movies = CreateLibrary();
        var dob = new DateOnly(2018, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(movies.AsQueryable(), dob, Today).ToList();

        result.Should().ContainSingle(m => m.Title == "Song");
    }

    [Test]
    public void Apply_ShouldHideSeasonAndEpisodeWhenParentSerieRatingExceedsAge()
    {
        var medias = CreateSerieLibrary();
        var dob = new DateOnly(2015, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(medias.AsQueryable(), dob, Today).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Kids Show", "Kids Season", "Kids Episode", "Song");
    }

    [Test]
    public void Apply_ShouldHideUnratedSerieAndItsSeasonAndEpisode()
    {
        var medias = CreateSerieLibrary();
        var dob = new DateOnly(2000, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(medias.AsQueryable(), dob, Today).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo(
            "Kids Show", "Kids Season", "Kids Episode", "Adult Show", "Adult Season", "Adult Episode", "Song");
    }

    [Test]
    public void Apply_ShouldKeepUnratedSerieAndItsSeasonAndEpisode_WhenHideUnratedIsOff()
    {
        var medias = CreateSerieLibrary();
        var dob = new DateOnly(2000, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(medias.AsQueryable(), dob, Today, hideUnrated: false).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo(
            "Kids Show", "Kids Season", "Kids Episode",
            "Adult Show", "Adult Season", "Adult Episode",
            "Unrated Show", "Unrated Season", "Unrated Episode",
            "Song");
    }

    [Test]
    public void Apply_ShouldKeepSeasonAndEpisodeWhenParentSerieRatingIsAllowed()
    {
        var medias = CreateSerieLibrary();
        var dob = new DateOnly(2012, 1, 1);

        var result = AgeRestrictionEvaluator.Apply(medias.AsQueryable(), dob, Today).ToList();

        result.Select(m => m.Title).Should().Contain(["Kids Show", "Kids Season", "Kids Episode"]);
        result.Select(m => m.Title).Should().NotContain(["Adult Show", "Adult Season", "Adult Episode"]);
    }

    [Test]
    public void IsActive_ShouldRequireEnabledFlagAndDateOfBirth()
    {
        AgeRestrictionEvaluator.IsActive(false, new DateOnly(2010, 1, 1)).Should().BeFalse();
        AgeRestrictionEvaluator.IsActive(true, null).Should().BeFalse();
        AgeRestrictionEvaluator.IsActive(true, new DateOnly(2010, 1, 1)).Should().BeTrue();
    }

    private static List<BaseMedia> CreateLibrary()
    {
        var family = new Movie { Id = Guid.NewGuid(), Title = "Family" };
        family.MetadataTags.Add(CreateRatingTag(family, "g", "G"));

        var teen = new Movie { Id = Guid.NewGuid(), Title = "Teen" };
        teen.MetadataTags.Add(CreateRatingTag(teen, "pg-13", "PG-13"));

        var unrated = new Movie { Id = Guid.NewGuid(), Title = "Unrated" };
        var song = new MusicTrack { Id = Guid.NewGuid(), Title = "Song" };

        return [family, teen, unrated, song];
    }

    private static List<BaseMedia> CreateSerieLibrary()
    {
        var kids = new Serie { Id = Guid.NewGuid(), Title = "Kids Show" };
        kids.MetadataTags.Add(CreateRatingTag(kids, "tv-y", "TV-Y"));
        var kidsSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Kids Season", Serie = kids, SerieId = kids.Id };
        var kidsEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Kids Episode", Serie = kids, SerieId = kids.Id };

        var adult = new Serie { Id = Guid.NewGuid(), Title = "Adult Show" };
        adult.MetadataTags.Add(CreateRatingTag(adult, "tv-ma", "TV-MA"));
        var adultSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Adult Season", Serie = adult, SerieId = adult.Id };
        var adultEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Adult Episode", Serie = adult, SerieId = adult.Id };

        var unrated = new Serie { Id = Guid.NewGuid(), Title = "Unrated Show" };
        var unratedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Unrated Season", Serie = unrated, SerieId = unrated.Id };
        var unratedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Unrated Episode", Serie = unrated, SerieId = unrated.Id };

        var song = new MusicTrack { Id = Guid.NewGuid(), Title = "Song" };

        return
        [
            kids, kidsSeason, kidsEpisode,
            adult, adultSeason, adultEpisode,
            unrated, unratedSeason, unratedEpisode,
            song
        ];
    }

    private static MediaMetadataTag CreateRatingTag(BaseMedia media, string key, string display) =>
        new()
        {
            Media = media,
            MetadataTag = new MetadataTag
            {
                Kind = MetadataTagKind.ContentRating,
                NormalizedKey = key,
                DisplayName = display
            }
        };
}
