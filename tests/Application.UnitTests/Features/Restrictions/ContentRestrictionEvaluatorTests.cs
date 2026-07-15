using K7.Server.Application.Features.Restrictions.Services;
using K7.Server.Domain.Entities.Medias;
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
                        Field = nameof(SmartPlaylistField.Title),
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
                        Field = nameof(SmartPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Forbidden"
                    }
                ]
            }
        };

        var result = ContentRestrictionEvaluator.GetRestricted(movies.AsQueryable(), profile).ToList();

        result.Should().ContainSingle(m => m.Title == "Forbidden");
    }

    private static List<BaseMedia> CreateMovies() =>
    [
        new Movie { Id = Guid.NewGuid(), Title = "Allowed" },
        new Movie { Id = Guid.NewGuid(), Title = "Forbidden" }
    ];
}
