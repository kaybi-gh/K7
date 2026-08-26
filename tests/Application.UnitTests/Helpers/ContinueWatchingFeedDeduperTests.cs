using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class ContinueWatchingFeedDeduperTests
{
    [Test]
    public void Deduplicate_ShouldKeepOneCard_WhenItemBookmarkAndSeriesNextUpShareEpisode()
    {
        var episodeId = Guid.NewGuid();
        var serieId = Guid.NewGuid();
        var newer = DateTime.UtcNow;
        var older = newer.AddHours(-2);

        var result = ContinueWatchingFeedDeduper.Deduplicate(
        [
            new ContinueWatchingFeedCandidate(episodeId, newer, serieId),
            new ContinueWatchingFeedCandidate(episodeId, older, episodeId)
        ]);

        result.Should().ContainSingle();
        result[0].MediaId.Should().Be(episodeId);
        result[0].GroupId.Should().Be(serieId);
        result[0].SortAt.Should().Be(newer);
    }

    [Test]
    public void Deduplicate_ShouldPreferInProgressEpisode_WhenSeriesNextUpIsNewer()
    {
        var serieId = Guid.NewGuid();
        var inProgressEpisodeId = Guid.NewGuid();
        var nextEpisodeId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var result = ContinueWatchingFeedDeduper.Deduplicate(
        [
            new ContinueWatchingFeedCandidate(
                inProgressEpisodeId,
                now.AddHours(-2),
                serieId,
                IsItemBookmark: true),
            new ContinueWatchingFeedCandidate(nextEpisodeId, now, serieId)
        ]);

        result.Should().ContainSingle();
        result[0].MediaId.Should().Be(inProgressEpisodeId);
        result[0].IsItemBookmark.Should().BeTrue();
    }

    [Test]
    public void Deduplicate_ShouldKeepOneCardPerSeries_WhenTwoEpisodesShareGroupId()
    {
        var serieId = Guid.NewGuid();
        var inProgressEpisodeId = Guid.NewGuid();
        var nextEpisodeId = Guid.NewGuid();
        var newer = DateTime.UtcNow;

        var result = ContinueWatchingFeedDeduper.Deduplicate(
        [
            new ContinueWatchingFeedCandidate(inProgressEpisodeId, newer, serieId),
            new ContinueWatchingFeedCandidate(nextEpisodeId, newer.AddHours(-1), serieId)
        ]);

        result.Should().ContainSingle();
        result[0].MediaId.Should().Be(inProgressEpisodeId);
    }

    [Test]
    public void Deduplicate_ShouldKeepSeparateMovies()
    {
        var movieA = Guid.NewGuid();
        var movieB = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var result = ContinueWatchingFeedDeduper.Deduplicate(
        [
            new ContinueWatchingFeedCandidate(movieA, now, movieA),
            new ContinueWatchingFeedCandidate(movieB, now.AddMinutes(-5), movieB)
        ]);

        result.Select(c => c.MediaId).Should().BeEquivalentTo([movieA, movieB]);
    }
}
