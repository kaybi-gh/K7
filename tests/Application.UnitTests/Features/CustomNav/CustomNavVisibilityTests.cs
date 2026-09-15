using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.CustomNav;

public class CustomNavVisibilityTests
{
    [Test]
    public void ShouldShowBar_ShouldBeTrue_OnExplore_WhenExploreEnabled()
    {
        var layout = EnabledBar();

        CustomNavVisibility.ShouldShowBar(layout, "/explore", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/explore?library-group=1", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/my-space", DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/settings", DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/movies/abc", DeviceType.Desktop).Should().BeFalse();
    }

    [Test]
    public void ShouldShowBar_ShouldFollowPageSwitches()
    {
        var layout = EnabledBar() with
        {
            ShowBarOnHome = false,
            ShowBarOnExplore = false,
            ShowBarOnMySpace = true,
            ShowBarOnSettings = true,
            ShowBarOnMedia = true
        };

        CustomNavVisibility.ShouldShowBar(layout, "/", DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/explore", DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/my-space/playlists", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/settings/account", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/movies/abc", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/library-groups/1", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/search", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/admin/dashboard", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/admin/navigation", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/k7/admin/navigation", DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowBar(layout, "/k7/settings/navigation", DeviceType.Desktop).Should().BeTrue();
    }

    [Test]
    public void ShouldShowBar_ShouldBeFalse_OnAuth()
    {
        var layout = EnabledBar() with
        {
            ShowBarOnHome = true,
            ShowBarOnExplore = true,
            ShowBarOnMySpace = true,
            ShowBarOnSettings = true,
            ShowBarOnMedia = true
        };

        CustomNavVisibility.ShouldShowBar(layout, "/sign-in", DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/auth/complete", DeviceType.Desktop).Should().BeFalse();
    }

    [Test]
    public void ShouldShowBar_ShouldBeFalse_OnPhoneAndTv()
    {
        var layout = EnabledBar() with { ShowOnPhone = true };

        CustomNavVisibility.ShouldShowBar(layout, "/", DeviceType.Phone).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/", DeviceType.Tablet).Should().BeFalse();
        CustomNavVisibility.ShouldShowBar(layout, "/", DeviceType.TV).Should().BeFalse();
    }

    [Test]
    public void ShouldShowBar_ShouldBeFalse_WhenDesktopIsDisabled()
    {
        var layout = EnabledBar() with { ShowOnDesktop = false };

        CustomNavVisibility.ShouldShowBar(layout, "/", DeviceType.Desktop).Should().BeFalse();
    }

    [Test]
    public void ShouldShowHomeRow_ShouldUseFeedRow_OrTvBarFallback()
    {
        var feed = Enabled(CustomNavPlacement.FeedRow);
        var bar = Enabled(CustomNavPlacement.Bar);

        CustomNavVisibility.ShouldShowHomeRow(feed, DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowHomeRow(bar, DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowHomeRow(bar, DeviceType.TV).Should().BeTrue();
    }

    [Test]
    public void ShouldShowHomeRow_ShouldNotUsePhoneBarFallback()
    {
        var bar = Enabled(CustomNavPlacement.Bar) with { ShowOnPhone = true };
        var feed = Enabled(CustomNavPlacement.FeedRow) with { ShowOnPhone = true };

        CustomNavVisibility.ShouldShowHomeRow(bar, DeviceType.Phone).Should().BeFalse();
        CustomNavVisibility.ShouldShowHomeRow(bar, DeviceType.Tablet).Should().BeFalse();
        CustomNavVisibility.ShouldShowHomeRow(feed, DeviceType.Phone).Should().BeTrue();
        CustomNavVisibility.ShouldShowHomeRow(Enabled(CustomNavPlacement.FeedRow), DeviceType.Phone).Should().BeFalse();
    }

    [Test]
    public void ShouldShowHomeRow_ShouldBeFalse_WhenDeviceIsDisabled()
    {
        var layout = Enabled(CustomNavPlacement.FeedRow) with { ShowOnDesktop = false, ShowOnTv = false };

        CustomNavVisibility.ShouldShowHomeRow(layout, DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowHomeRow(layout, DeviceType.TV).Should().BeFalse();
    }

    [Test]
    public void ShouldShowGroupFeedRow_ShouldBeFalse_WhenScopeIsHome()
    {
        var layout = Enabled(CustomNavPlacement.FeedRow);

        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.Desktop).Should().BeFalse();
    }

    [Test]
    public void ShouldShowGroupFeedRow_ShouldBeTrue_WhenExploreFeedsEnabled()
    {
        var layout = Enabled(CustomNavPlacement.FeedRow) with
        {
            ShowRowOnExploreFeeds = true
        };

        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.Desktop).Should().BeTrue();
        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.TV).Should().BeTrue();
        CustomNavVisibility.ShouldShowGroupFeedRow(Enabled(CustomNavPlacement.Bar) with
        {
            ShowRowOnExploreFeeds = true
        }, DeviceType.Desktop).Should().BeFalse();
    }

    [Test]
    public void ShouldShowHomeRow_ShouldBeFalse_WhenOnlyExploreFeedsEnabled()
    {
        var layout = Enabled(CustomNavPlacement.FeedRow) with
        {
            ShowRowOnHome = false,
            ShowRowOnExploreFeeds = true
        };

        CustomNavVisibility.ShouldShowHomeRow(layout, DeviceType.Desktop).Should().BeFalse();
        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.Desktop).Should().BeTrue();
    }

    [Test]
    public void ShouldShowGroupFeedRow_ShouldBeFalse_OnPhone()
    {
        var layout = Enabled(CustomNavPlacement.FeedRow) with
        {
            ShowRowOnExploreFeeds = true,
            ShowOnPhone = true
        };

        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.Phone).Should().BeFalse();
        CustomNavVisibility.ShouldShowGroupFeedRow(layout, DeviceType.Tablet).Should().BeFalse();
    }

    [Test]
    public void IsActive_ShouldMatchNestedPaths_ExceptHome()
    {
        CustomNavVisibility.IsActive("/my-space", "/my-space/playlists").Should().BeTrue();
        CustomNavVisibility.IsActive("/", "/explore").Should().BeFalse();
        CustomNavVisibility.IsActive("/", "/").Should().BeTrue();
    }

    private static CustomNavLayoutDto EnabledBar() =>
        Enabled(CustomNavPlacement.Bar);

    private static CustomNavLayoutDto Enabled(CustomNavPlacement placement) => new()
    {
        Enabled = true,
        Placement = placement,
        BarShowIcons = true,
        Items =
        [
            new CustomNavItemDto
            {
                Id = Guid.NewGuid(),
                Kind = CustomNavItemKind.AppRoute,
                Route = "/search"
            }
        ]
    };
}
