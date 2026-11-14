using K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;
using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.UnitTests.Features.Home.Queries;

[TestFixture]
public class HomeFeedRecentlyAddedStrategyTests
{
    private static Movie Movie(string title) => new() { Id = Guid.NewGuid(), Title = title, SortTitle = title };

    private static List<SerieEpisode> Episodes(string serieTitle, int count)
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = serieTitle, SortTitle = serieTitle };
        var season = new SerieSeason
        {
            Id = Guid.NewGuid(),
            SerieId = serie.Id,
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1",
            SortTitle = "Season 1"
        };

        return Enumerable.Range(1, count)
            .Select(number => new SerieEpisode
            {
                Id = Guid.NewGuid(),
                SerieId = serie.Id,
                Serie = serie,
                SeasonId = season.Id,
                Season = season,
                EpisodeNumber = number,
                Title = $"E{number}",
                SortTitle = $"E{number}"
            })
            .ToList();
    }

    [Test]
    public void CountCards_ShouldCountASeasonOfEpisodesAsOneCard()
    {
        // Thirty new episodes of one series fill the first window of the feed, and make one card.
        HomeFeedRecentlyAddedStrategy.CountCards(Episodes("Show", 30)).Should().Be(1);
    }

    [Test]
    public void CountCards_ShouldCountEachMovieAndEachSeries()
    {
        var items = new List<BaseMedia>();
        items.AddRange(Episodes("Show A", 12));
        items.AddRange(Episodes("Show B", 3));
        items.Add(Movie("Movie A"));
        items.Add(Movie("Movie B"));

        HomeFeedRecentlyAddedStrategy.CountCards(items).Should().Be(4);
    }

    [Test]
    public void CountCards_ShouldIgnoreSeriesAndSeasonsThemselves()
    {
        var episodes = Episodes("Show", 2);
        var items = new List<BaseMedia> { episodes[0].Serie, episodes[0].Season };
        items.AddRange(episodes);

        // Episodes stand for their series: the series and season entries add no card.
        HomeFeedRecentlyAddedStrategy.CountCards(items).Should().Be(1);
    }

    [Test]
    public void CountCards_ShouldBeZero_WhenThereIsNothing()
    {
        HomeFeedRecentlyAddedStrategy.CountCards([]).Should().Be(0);
    }
}
