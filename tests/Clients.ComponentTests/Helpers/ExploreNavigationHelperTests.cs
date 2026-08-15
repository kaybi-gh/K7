using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class ExploreNavigationHelperTests
{
    [Test]
    public void GetCategoryHref_ShouldReturnExploreFeed_WhenActionIsSuggestions()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        ExploreNavigationHelper.GetCategoryHref(groupId, ExploreTapAction.Suggestions)
            .Should().Be("/explore?library-group=11111111-1111-1111-1111-111111111111");
    }

    [Test]
    public void GetCategoryHref_ShouldReturnLibraryBrowse_WhenActionIsBrowse()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        ExploreNavigationHelper.GetCategoryHref(groupId, ExploreTapAction.Browse)
            .Should().Be("/library-groups/11111111-1111-1111-1111-111111111111");
    }

    [Test]
    public void ResolveTapAction_ShouldPreferUserOverride()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var preferences = new GeneralPreferencesDto
        {
            ExploreTapActions = { [groupId] = ExploreTapAction.Browse }
        };

        ExploreNavigationHelper.ResolveTapAction(groupId, ExploreTapAction.Suggestions, preferences)
            .Should().Be(ExploreTapAction.Browse);
    }

    [Test]
    public void ResolveTapAction_ShouldUseGroupDefault_WhenNoOverride()
    {
        var groupId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        ExploreNavigationHelper.ResolveTapAction(groupId, ExploreTapAction.Browse, new GeneralPreferencesDto())
            .Should().Be(ExploreTapAction.Browse);
    }
}
