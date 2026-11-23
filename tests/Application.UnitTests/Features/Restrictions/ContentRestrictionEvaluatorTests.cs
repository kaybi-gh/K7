using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Application.UnitTests.Features.Restrictions;

public class ContentRestrictionEvaluatorTests
{
    [Test]
    public void ApplyRestriction_ShouldReturnUnchangedQuery_WhenNoRules()
    {
        var movies = CreateMovies();
        var profile = new ContentRestrictionProfile { Name = "None" };

        var result = ContentRestrictionEvaluator.ApplyRestriction(movies.AsQueryable(), profile).ToList();

        result.Should().HaveCount(2);
    }

    [Test]
    public void ApplyRestriction_ShouldExcludeMatchingTitles()
    {
        var movies = CreateMovies();
        var profile = new ContentRestrictionProfile
        {
            Name = "Block Forbidden",
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Forbidden"
                    }
                ]
            }
        };

        var result = ContentRestrictionEvaluator.ApplyRestriction(movies.AsQueryable(), profile).ToList();

        result.Should().ContainSingle(m => m.Title == "Allowed");
    }

    [Test]
    public void GetRestricted_ShouldReturnEmpty_WhenNoRules()
    {
        var movies = CreateMovies();
        var profile = new ContentRestrictionProfile { Name = "None" };

        var result = ContentRestrictionEvaluator.GetRestricted(movies.AsQueryable(), profile).ToList();

        result.Should().BeEmpty();
    }

    [Test]
    public void GetRestricted_ShouldReturnOnlyMatchingItems()
    {
        var movies = CreateMovies();
        var profile = new ContentRestrictionProfile
        {
            Name = "Block Forbidden",
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Forbidden"
                    }
                ]
            }
        };

        var result = ContentRestrictionEvaluator.GetRestricted(movies.AsQueryable(), profile).ToList();

        result.Should().ContainSingle(m => m.Title == "Forbidden");
    }

    [Test]
    public void ApplyRestriction_ShouldKeepSeasonAndEpisode_WhenParentSerieRatingIsAllowed()
    {
        var medias = CreateSerieLibrary();
        var profile = ContentRatingProfile(RuleMatchCondition.All,
        [
            NotEqualsRating("9"),
            NotEqualsRating("10")
        ]);

        var result = ContentRestrictionEvaluator.ApplyRestriction(medias.AsQueryable(), profile).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Allowed Show", "Allowed Season", "Allowed Episode");
    }

    [Test]
    public void ApplyRestriction_ShouldHideSeasonAndEpisode_WhenParentSerieRatingIsBlocked()
    {
        var medias = CreateSerieLibrary();
        var profile = ContentRatingProfile(RuleMatchCondition.Any, [EqualsRating("8")]);

        var result = ContentRestrictionEvaluator.ApplyRestriction(medias.AsQueryable(), profile).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo(
            "Allowed Show", "Allowed Season", "Allowed Episode",
            "Unrated Show", "Unrated Season", "Unrated Episode",
            "Unrated Movie");
    }

    [Test]
    public void ApplyRestriction_ShouldTreatSeasonAndEpisodeAsEmpty_WhenParentSerieHasNoRating()
    {
        var medias = CreateSerieLibrary();
        var profile = ContentRatingProfile(RuleMatchCondition.Any, [new ConditionRuleItem
        {
            Field = nameof(RestrictionField.ContentRating),
            Operator = RuleOperator.IsEmpty
        }]);

        var result = ContentRestrictionEvaluator.ApplyRestriction(medias.AsQueryable(), profile).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo(
            "Allowed Show", "Allowed Season", "Allowed Episode", "Blocked Show", "Blocked Season", "Blocked Episode");
    }

    [Test]
    public void ApplyRestriction_ShouldIgnoreContentRatingStoredOnSeasonOrEpisode()
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = "Bare Show" };
        var season = new SerieSeason { Id = Guid.NewGuid(), Title = "Tagged Season", Serie = serie, SerieId = serie.Id };
        season.MetadataTags.Add(CreateRatingTag(season, "10", "10"));
        var medias = new List<BaseMedia> { serie, season };
        var profile = ContentRatingProfile(RuleMatchCondition.Any, [EqualsRating("10")]);

        var result = ContentRestrictionEvaluator.ApplyRestriction(medias.AsQueryable(), profile).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Bare Show", "Tagged Season");
    }

    [TestCase("Network", MetadataTagKind.Network)]
    [TestCase("Studio", MetadataTagKind.Studio)]
    public void ApplyRestriction_ShouldInheritSerieOwnedTag_WhenMediaIsSeasonOrEpisode(string field, MetadataTagKind kind)
    {
        var allowed = new Serie { Id = Guid.NewGuid(), Title = "Allowed Show" };
        allowed.MetadataTags.Add(CreateTag(allowed, kind, "hbo", "HBO"));
        var allowedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Allowed Season", Serie = allowed, SerieId = allowed.Id };
        var allowedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Allowed Episode", Serie = allowed, SerieId = allowed.Id };

        var blocked = new Serie { Id = Guid.NewGuid(), Title = "Blocked Show" };
        blocked.MetadataTags.Add(CreateTag(blocked, kind, "other", "Other"));
        var blockedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Blocked Season", Serie = blocked, SerieId = blocked.Id };
        var blockedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Blocked Episode", Serie = blocked, SerieId = blocked.Id };

        var medias = new List<BaseMedia> { allowed, allowedSeason, allowedEpisode, blocked, blockedSeason, blockedEpisode };
        var profile = TagProfile(field, RuleOperator.Equals, "HBO");

        var result = ContentRestrictionEvaluator.ApplyRestriction(medias.AsQueryable(), profile).ToList();

        result.Select(m => m.Title).Should().BeEquivalentTo("Blocked Show", "Blocked Season", "Blocked Episode");
    }

    private static List<BaseMedia> CreateMovies() =>
    [
        new Movie { Id = Guid.NewGuid(), Title = "Allowed" },
        new Movie { Id = Guid.NewGuid(), Title = "Forbidden" }
    ];

    private static List<BaseMedia> CreateSerieLibrary()
    {
        var allowed = new Serie { Id = Guid.NewGuid(), Title = "Allowed Show" };
        allowed.MetadataTags.Add(CreateRatingTag(allowed, "10", "10"));
        var allowedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Allowed Season", Serie = allowed, SerieId = allowed.Id };
        var allowedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Allowed Episode", Serie = allowed, SerieId = allowed.Id };

        var blocked = new Serie { Id = Guid.NewGuid(), Title = "Blocked Show" };
        blocked.MetadataTags.Add(CreateRatingTag(blocked, "8", "8"));
        var blockedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Blocked Season", Serie = blocked, SerieId = blocked.Id };
        var blockedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Blocked Episode", Serie = blocked, SerieId = blocked.Id };

        var unrated = new Serie { Id = Guid.NewGuid(), Title = "Unrated Show" };
        var unratedSeason = new SerieSeason { Id = Guid.NewGuid(), Title = "Unrated Season", Serie = unrated, SerieId = unrated.Id };
        var unratedEpisode = new SerieEpisode { Id = Guid.NewGuid(), Title = "Unrated Episode", Serie = unrated, SerieId = unrated.Id };

        var unratedMovie = new Movie { Id = Guid.NewGuid(), Title = "Unrated Movie" };

        return
        [
            allowed, allowedSeason, allowedEpisode,
            blocked, blockedSeason, blockedEpisode,
            unrated, unratedSeason, unratedEpisode,
            unratedMovie
        ];
    }

    private static ContentRestrictionProfile ContentRatingProfile(
        RuleMatchCondition match,
        IReadOnlyList<ConditionRuleItem> rules) =>
        new()
        {
            Name = "Ratings",
            RuleFilter = new RuleGroup
            {
                MatchCondition = match,
                Items = [.. rules]
            }
        };

    private static ConditionRuleItem EqualsRating(string value) => new()
    {
        Field = nameof(RestrictionField.ContentRating),
        Operator = RuleOperator.Equals,
        Value = value
    };

    private static ConditionRuleItem NotEqualsRating(string value) => new()
    {
        Field = nameof(RestrictionField.ContentRating),
        Operator = RuleOperator.NotEquals,
        Value = value
    };

    private static ContentRestrictionProfile TagProfile(string field, RuleOperator op, string value) =>
        new()
        {
            Name = field,
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = field,
                        Operator = op,
                        Value = value
                    }
                ]
            }
        };

    private static MediaMetadataTag CreateTag(BaseMedia media, MetadataTagKind kind, string key, string display) =>
        new()
        {
            Media = media,
            MetadataTag = new MetadataTag
            {
                Kind = kind,
                NormalizedKey = key,
                DisplayName = display
            }
        };

    private static MediaMetadataTag CreateRatingTag(BaseMedia media, string key, string display) =>
        CreateTag(media, MetadataTagKind.ContentRating, key, display);
}
