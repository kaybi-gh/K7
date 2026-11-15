using K7.Shared.Dtos.Home;
using K7.Shared.Enums;
using K7.Shared.Home;

namespace K7.Server.Application.UnitTests.Features.Home.Services;

[TestFixture]
public class HomeLayoutRulesTests
{
    private static HomeRowConfigDto Row(string title, int order, HomeRowDisplayType displayType = HomeRowDisplayType.Carousel) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        DisplayType = displayType,
        AutoAdvanceSeconds = displayType.IsBanner() ? 8 : null,
        PageSize = 20,
        ContinueWatching = false,
        IsVisible = true,
        Order = order
    };

    [Test]
    public void HasValidBanner_ShouldPass_WhenThereIsNoBanner()
    {
        HomeLayoutRules.HasValidBanner([Row("A", 0), Row("B", 1)]).Should().BeTrue();
        HomeLayoutRules.HasValidBanner([]).Should().BeTrue();
    }

    [Test]
    public void HasValidBanner_ShouldPass_WhenTheOnlyBannerIsFirst()
    {
        HomeLayoutRules.HasValidBanner([Row("Banner", 0, HomeRowDisplayType.FeaturedBleed), Row("B", 1)])
            .Should().BeTrue();
    }

    [Test]
    public void HasValidBanner_ShouldFail_WhenTheBannerIsNotFirst()
    {
        HomeLayoutRules.HasValidBanner([Row("A", 0), Row("Banner", 1, HomeRowDisplayType.Featured)])
            .Should().BeFalse();
    }

    [Test]
    public void HasValidBanner_ShouldFail_WhenThereAreTwoBanners()
    {
        HomeLayoutRules.HasValidBanner(
        [
            Row("Banner", 0, HomeRowDisplayType.Featured),
            Row("Other", 1, HomeRowDisplayType.FeaturedBleed)
        ]).Should().BeFalse();
    }

    [Test]
    public void ApplyBannerRule_ShouldMoveTheBannerFirst_AndKeepTheRestInOrder()
    {
        var rows = HomeLayoutRules.ApplyBannerRule(
        [
            Row("A", 0),
            Row("B", 1),
            Row("Banner", 2, HomeRowDisplayType.FeaturedBleed)
        ]);

        rows.Select(r => r.Title).Should().Equal("Banner", "A", "B");
        rows.Select(r => r.Order).Should().Equal(0, 1, 2);
        HomeLayoutRules.HasValidBanner(rows).Should().BeTrue();
    }

    [Test]
    public void ApplyBannerRule_ShouldTurnExtraBannersIntoCarousels()
    {
        var rows = HomeLayoutRules.ApplyBannerRule(
        [
            Row("First", 0, HomeRowDisplayType.Featured),
            Row("Second", 1, HomeRowDisplayType.FeaturedBleed)
        ]);

        rows[0].DisplayType.Should().Be(HomeRowDisplayType.Featured);
        rows[1].DisplayType.Should().Be(HomeRowDisplayType.Carousel);
        rows[1].AutoAdvanceSeconds.Should().BeNull();
    }

    [Test]
    public void ApplyBannerRule_ShouldOnlyRenumber_WhenThereIsNoBanner()
    {
        var rows = HomeLayoutRules.ApplyBannerRule([Row("B", 5), Row("A", 2)]);

        rows.Select(r => r.Title).Should().Equal("A", "B");
        rows.Select(r => r.Order).Should().Equal(0, 1);
    }
}
