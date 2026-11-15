using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class HomeRowSummaryTests
{
    private static readonly Guid _groupA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _groupB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid _groupC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    private static readonly List<LibraryGroupDto> _groups =
    [
        new() { Id = _groupA, Title = "Movies", MediaType = default },
        new() { Id = _groupB, Title = "Series", MediaType = default },
        new() { Id = _groupC, Title = "Kids", MediaType = default }
    ];

    private const string Dot = HomeRowSummary.Separator;

    // Keys come back as their own name, with arguments appended: enough to read the summary.
    private static IStringLocalizer Localizer(params string[] missing)
    {
        var localizer = Substitute.For<IStringLocalizer>();
        localizer[Arg.Any<string>()].Returns(call =>
        {
            var key = call.Arg<string>();
            return new LocalizedString(key, key, resourceNotFound: missing.Contains(key));
        });
        return localizer;
    }

    private static IStringLocalizer FormatLocalizer()
    {
        var localizer = Localizer();
        localizer["SummaryItems"].Returns(new LocalizedString("SummaryItems", "{0} items"));
        localizer["SummaryMore"].Returns(new LocalizedString("SummaryMore", "+{0}"));
        localizer["AutoAdvanceSeconds"].Returns(new LocalizedString("AutoAdvanceSeconds", "every {0} s"));
        return localizer;
    }

    private static HomeRowEditModel Row() => new()
    {
        Id = Guid.NewGuid(),
        Title = "Row",
        OrderBy = MediaOrderingOption.CreatedDesc,
        PageSize = 20
    };

    [Test]
    public void Display_ShouldSayCarouselAndItsFormat()
    {
        var landscape = Row();
        landscape.DisplayType = HomeRowDisplayType.Landscape;

        HomeRowSummary.Display(Row(), FormatLocalizer(), FormatLocalizer())
            .Should().Be($"KindCarousel{Dot}DisplayCarousel{Dot}20 items");
        HomeRowSummary.Display(landscape, FormatLocalizer(), FormatLocalizer())
            .Should().Be($"KindCarousel{Dot}DisplayLandscape{Dot}20 items");
    }

    [Test]
    public void Display_ShouldSayBannerItsStyleAndItsAutoAdvance()
    {
        var banner = Row();
        banner.DisplayType = HomeRowDisplayType.FeaturedBleed;
        banner.PageSize = 5;
        banner.AutoAdvanceSeconds = 8;

        HomeRowSummary.Display(banner, FormatLocalizer(), FormatLocalizer())
            .Should().Be($"KindBanner{Dot}BannerBleed{Dot}every 8 s{Dot}5 items");

        banner.DisplayType = HomeRowDisplayType.Featured;
        banner.AutoAdvanceSeconds = null;
        HomeRowSummary.Display(banner, FormatLocalizer(), FormatLocalizer())
            .Should().Be($"KindBanner{Dot}BannerContained{Dot}5 items");
    }

    [Test]
    public void Content_ShouldSayScopeMediaTypesAndSort()
    {
        var row = Row();
        row.MediaTypes = [MediaType.Movie, MediaType.Serie];

        HomeRowSummary.Content(row, [], _groups, FormatLocalizer(), FormatLocalizer())
            .Should().Be($"ScopeAll{Dot}MediaTypeMovie, MediaTypeSerie{Dot}OrderCreatedDesc");
    }

    [Test]
    public void Content_ShouldNameTheFirstLibraryGroups_ThenCountTheRest()
    {
        var row = Row();
        row.LibraryGroupIds = [_groupA, _groupB, _groupC];

        HomeRowSummary.Content(row, [], _groups, FormatLocalizer(), FormatLocalizer())
            .Should().StartWith($"Movies, Series +1{Dot}");
    }

    [Test]
    public void Content_ShouldFallBackToTheKindOfScope_WhenNamesAreNotKnown()
    {
        var row = Row();
        row.LibraryIds = [Guid.NewGuid()];

        HomeRowSummary.Content(row, [], [], FormatLocalizer(), FormatLocalizer())
            .Should().StartWith($"ScopeLibraries{Dot}");
    }

    [Test]
    public void Content_ShouldOnlySayContinueWatching_ForAContinueWatchingRow()
    {
        var row = Row();
        row.ContinueWatching = true;

        HomeRowSummary.Content(row, [], _groups, FormatLocalizer(), FormatLocalizer()).Should().Be("ContinueWatching");
    }

    [Test]
    public void Content_ShouldLeaveOutASortTheDialogDoesNotOffer()
    {
        var row = Row();
        row.OrderBy = MediaOrderingOption.RecommendedForYou;

        HomeRowSummary.Content(row, [], _groups, Localizer("OrderRecommendedForYou"), FormatLocalizer())
            .Should().Be("ScopeAll");
    }
}
