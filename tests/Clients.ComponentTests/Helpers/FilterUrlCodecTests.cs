using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.Rules;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class FilterUrlCodecTests
{
    [Test]
    public void EncodeDecode_ShouldRoundTripRuleGroup()
    {
        var filter = new RuleGroupDto
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItemDto
                {
                    Field = nameof(SmartPlaylistField.Genre),
                    Operator = RuleOperator.Contains,
                    Value = "Sci-Fi"
                },
                new NestedGroupItemDto
                {
                    MatchCondition = RuleMatchCondition.Any,
                    Items =
                    [
                        new ConditionRuleItemDto
                        {
                            Field = nameof(SmartPlaylistField.Year),
                            Operator = RuleOperator.GreaterThan,
                            Value = "2020"
                        }
                    ]
                }
            ]
        };

        var encoded = FilterUrlCodec.Encode(filter);
        encoded.Should().NotBeNullOrWhiteSpace();

        var decoded = FilterUrlCodec.Decode<RuleGroupDto>(encoded);
        decoded.Should().BeEquivalentTo(filter);
    }

    [Test]
    public void BuildBrowseUrl_ShouldIncludeEncodedFilter()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var filter = new RuleGroupDto
        {
            MatchCondition = RuleMatchCondition.All,
            Items =
            [
                new ConditionRuleItemDto
                {
                    Field = nameof(SmartPlaylistField.Genre),
                    Operator = RuleOperator.Equals,
                    Value = "Drama"
                }
            ]
        };

        var url = LibraryGroupBrowseNavigationHelper.BuildBrowseUrl(
            groupId,
            new LibraryGroupBrowseUrlState(
                MediaType: MediaType.Movie,
                Sort: MediaOrderingOption.ReleaseDateDesc,
                View: BrowseViewMode.List,
                Filter: filter));

        url.Should().StartWith($"/library-groups/{groupId}?");
        url.Should().Contain("mediaType=Movie");
        url.Should().Contain("sort=releaseDateDesc");
        url.Should().Contain("view=list");
        url.Should().Contain("filter=");

        var query = url[(url.IndexOf('?') + 1)..]
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]), StringComparer.OrdinalIgnoreCase);

        var parsed = LibraryGroupBrowseNavigationHelper.ParseBrowseState(query);
        parsed.MediaType.Should().Be(MediaType.Movie);
        parsed.Sort.Should().Be(MediaOrderingOption.ReleaseDateDesc);
        parsed.View.Should().Be(BrowseViewMode.List);
        parsed.Filter.Should().BeEquivalentTo(filter);
    }
}
