using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.UnitTests.Services;

public class ContinueWatchingEpisodeSelectorTests
{
    private static readonly Guid SerieId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Test]
    public void DeduplicateBySerie_ShouldKeepEarliestEnqueuedEpisode_WhenMultipleNextEpisodesExist()
    {
        // Arrange: completed ep 1 enqueued ep 2, completed ep 4 enqueued ep 5
        var episodes = new List<BaseMedia>
        {
            CreateEpisode(2, enqueuedAt: DateTime.UtcNow.AddHours(-2)),
            CreateEpisode(5, enqueuedAt: DateTime.UtcNow.AddHours(-1)),
        };

        // Act
        var result = ContinueWatchingEpisodeSelector.DeduplicateBySerie(episodes);

        // Assert
        result.Should().ContainSingle();
        ((SerieEpisode)result[0]).EpisodeNumber.Should().Be(2);
    }

    [Test]
    public void DeduplicateBySerie_ShouldKeepMostRecentInProgressEpisode_WhenMultipleArePartiallyWatched()
    {
        // Arrange
        var older = CreateEpisode(2, enqueuedAt: DateTime.UtcNow.AddHours(-3), progress: 40);
        var newer = CreateEpisode(5, enqueuedAt: DateTime.UtcNow.AddHours(-1), progress: 60);

        // Act
        var result = ContinueWatchingEpisodeSelector.DeduplicateBySerie([older, newer]);

        // Assert
        result.Should().ContainSingle();
        ((SerieEpisode)result[0]).EpisodeNumber.Should().Be(5);
    }

    [Test]
    public void DeduplicateBySerie_ShouldKeepInProgressOverEnqueued_WhenBothExist()
    {
        // Arrange
        var enqueued = CreateEpisode(2, enqueuedAt: DateTime.UtcNow);
        var inProgress = CreateEpisode(5, enqueuedAt: DateTime.UtcNow.AddHours(-1), progress: 25);

        // Act
        var result = ContinueWatchingEpisodeSelector.DeduplicateBySerie([enqueued, inProgress]);

        // Assert
        result.Should().ContainSingle();
        ((SerieEpisode)result[0]).EpisodeNumber.Should().Be(5);
    }

    [Test]
    public void DeduplicateBySerie_ShouldKeepOneEntryPerSerie_AndPreserveMovies()
    {
        // Arrange
        var serieA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var serieB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Movie" };
        movie.UserMediaStates.Add(CreateState(DateTime.UtcNow));

        var items = new List<BaseMedia>
        {
            CreateEpisode(2, serieId: serieA, enqueuedAt: DateTime.UtcNow.AddHours(-1)),
            CreateEpisode(5, serieId: serieA, enqueuedAt: DateTime.UtcNow),
            CreateEpisode(1, serieId: serieB, enqueuedAt: DateTime.UtcNow.AddHours(-2)),
            CreateEpisode(3, serieId: serieB, enqueuedAt: DateTime.UtcNow.AddHours(-3)),
            movie,
        };

        // Act
        var result = ContinueWatchingEpisodeSelector.DeduplicateBySerie(items);

        // Assert
        result.Should().HaveCount(3);
        result.OfType<SerieEpisode>().Select(e => e.SerieId).Should().BeEquivalentTo([serieA, serieB]);
        result.OfType<SerieEpisode>().Single(e => e.SerieId == serieA).EpisodeNumber.Should().Be(2);
        result.OfType<SerieEpisode>().Single(e => e.SerieId == serieB).EpisodeNumber.Should().Be(1);
        result.OfType<Movie>().Should().ContainSingle();
    }

    private static SerieEpisode CreateEpisode(
        int episodeNumber,
        Guid? serieId = null,
        DateTime? enqueuedAt = null,
        double progress = 0)
    {
        var episode = new SerieEpisode
        {
            Id = Guid.NewGuid(),
            SerieId = serieId ?? SerieId,
            EpisodeNumber = episodeNumber,
            Season = new SerieSeason
            {
                SeasonNumber = 1,
                SerieId = serieId ?? SerieId,
            },
        };
        episode.UserMediaStates.Add(CreateState(enqueuedAt ?? DateTime.UtcNow, progress));
        return episode;
    }

    private static UserMediaState CreateState(DateTime lastInteractedAt, double progress = 0) => new()
    {
        LastInteractedAt = lastInteractedAt,
        ProgressPercentage = progress,
        LastPlaybackPosition = progress > 0 ? 120 : 0,
        IsCompleted = false,
    };
}
