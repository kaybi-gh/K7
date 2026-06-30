using K7.Shared.Home;

namespace K7.Server.Application.UnitTests.Features.Home.Services;

public class HomeLayoutRowTitlesTests
{
    [Test]
    public void NewlyAddedIn_ShouldUseParseablePrefix()
    {
        var title = HomeLayoutRowTitles.NewlyAddedIn("Films");

        title.Should().Be("NewlyAddedIn|Films");
        HomeLayoutRowTitles.TryParseNewlyAddedIn(title, out var scope).Should().BeTrue();
        scope.Should().Be("Films");
    }

    [Test]
    public void TryParseNewlyAddedIn_ShouldReturnFalse_ForOtherTitles()
    {
        HomeLayoutRowTitles.TryParseNewlyAddedIn("ContinueWatching", out _).Should().BeFalse();
        HomeLayoutRowTitles.TryParseNewlyAddedIn("Custom carousel", out _).Should().BeFalse();
    }
}
