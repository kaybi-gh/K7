using K7.Server.Application.Common.Services;

namespace K7.Server.Application.UnitTests.Services;

public class ContinueWatchingEpisodeOrderTests
{
    [Test]
    public void GetSortKey_ShouldOrderSeasonsThenEpisodes()
    {
        var s1e2 = ContinueWatchingEpisodeOrder.GetSortKey(1, 2);
        var s1e5 = ContinueWatchingEpisodeOrder.GetSortKey(1, 5);
        var s2e1 = ContinueWatchingEpisodeOrder.GetSortKey(2, 1);

        s1e2.Should().BeLessThan(s1e5);
        s1e5.Should().BeLessThan(s2e1);
    }

    [Test]
    public void GetSortKey_ShouldTreatSeasonZeroAsLast()
    {
        var specials = ContinueWatchingEpisodeOrder.GetSortKey(0, 1);
        var seasonTwo = ContinueWatchingEpisodeOrder.GetSortKey(2, 99);

        specials.Should().BeGreaterThan(seasonTwo);
    }
}
