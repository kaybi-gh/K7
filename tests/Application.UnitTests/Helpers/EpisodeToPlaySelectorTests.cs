using K7.Server.Application.Helpers;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.UnitTests.Helpers;

public class EpisodeToPlaySelectorTests
{
    [Test]
    public void Select_ShouldReturnNull_WhenThereIsNoCandidate()
    {
        EpisodeToPlaySelector.Select([]).Should().BeNull();
    }

    [Test]
    public void Select_ShouldReturnMostRecentInProgressEpisode_WhenSeveralAreStarted()
    {
        var older = Candidate(1, 2, InProgress(DateTime.UtcNow.AddDays(-2)));
        var recent = Candidate(2, 5, InProgress(DateTime.UtcNow));

        EpisodeToPlaySelector.Select([Candidate(1, 1, Completed()), older, recent]).Should().Be(recent.Id);
    }

    [Test]
    public void Select_ShouldTreatPartialProgressAsInProgress_WhenPositionIsUnknown()
    {
        var partial = Candidate(1, 3, new UserMediaStateDto { ProgressPercentage = 40, LastInteractedAt = DateTime.UtcNow });

        EpisodeToPlaySelector.Select([Candidate(1, 1, null), partial]).Should().Be(partial.Id);
    }

    [Test]
    public void Select_ShouldReturnFirstUnwatchedEpisode_WhenNothingIsInProgress()
    {
        var unwatched = Candidate(2, 1, null);

        EpisodeToPlaySelector.Select([Candidate(1, 2, Completed()), unwatched, Candidate(1, 1, Completed())])
            .Should().Be(unwatched.Id);
    }

    [Test]
    public void Select_ShouldReturnFirstEpisode_WhenEverythingIsWatched()
    {
        var first = Candidate(1, 1, Completed());

        EpisodeToPlaySelector.Select([Candidate(1, 2, Completed()), first]).Should().Be(first.Id);
    }

    [Test]
    public void Select_ShouldOrderSpecialsAfterRegularSeasons()
    {
        var regular = Candidate(1, 1, null);

        EpisodeToPlaySelector.Select([Candidate(0, 1, null), regular]).Should().Be(regular.Id);
    }

    [Test]
    public void Select_ShouldIgnoreCompletedEpisode_WhenItStillHasAPosition()
    {
        var completedWithPosition = Candidate(1, 1, Completed() with { LastPlaybackPosition = 120, LastInteractedAt = DateTime.UtcNow });
        var next = Candidate(1, 2, null);

        EpisodeToPlaySelector.Select([completedWithPosition, next]).Should().Be(next.Id);
    }

    private static EpisodeToPlayCandidate Candidate(int season, int episode, UserMediaStateDto? state) =>
        new(Guid.NewGuid(), season, episode, state);

    private static UserMediaStateDto InProgress(DateTime lastInteractedAt) =>
        new() { LastPlaybackPosition = 600, ProgressPercentage = 25, LastInteractedAt = lastInteractedAt };

    private static UserMediaStateDto Completed() =>
        new() { IsCompleted = true, ProgressPercentage = 100 };
}
