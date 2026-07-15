using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.UnitTests.Entities.Users;

[TestFixture]
public class UserMediaStateTests
{
    [Test]
    public void RecordProgress_ShouldMarkCompletedAndResetPosition_WhenThresholdReached()
    {
        var state = new UserMediaState();
        var media = new Movie { Title = "Film" };
        var policy = new PlaybackProgressPolicy(IsMusic: false, CompletedThresholdPercent: 90, CompletedMinDurationSeconds: 0);
        var now = new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);

        var result = state.RecordProgress(5400, 6000, policy, media, now);

        result.IsCompleted.Should().BeTrue();
        result.WasNewlyCompleted.Should().BeTrue();
        result.ProgressPercentage.Should().Be(100);
        state.PlayCount.Should().Be(1);
        state.LastPlaybackPosition.Should().Be(0);
        state.LastInteractedAt.Should().Be(now);
    }

    [Test]
    public void RecordProgress_ShouldStorePartialProgress_WhenNotCompleted()
    {
        var state = new UserMediaState();
        var media = new Movie { Title = "Film" };
        var policy = new PlaybackProgressPolicy(IsMusic: false, CompletedThresholdPercent: 90, CompletedMinDurationSeconds: 0);

        var result = state.RecordProgress(3000, 6000, policy, media, DateTime.UtcNow);

        result.IsCompleted.Should().BeFalse();
        result.WasNewlyCompleted.Should().BeFalse();
        result.ProgressPercentage.Should().Be(50);
        state.LastPlaybackPosition.Should().Be(3000);
        state.PlayCount.Should().Be(0);
    }

    [Test]
    public void RecordProgress_ShouldNotIncrementPlayCountAgain_WhenAlreadyCompleted()
    {
        var state = new UserMediaState { IsCompleted = true, PlayCount = 2 };
        var media = new Movie { Title = "Film" };
        var policy = new PlaybackProgressPolicy(IsMusic: false, CompletedThresholdPercent: 90, CompletedMinDurationSeconds: 0);

        var result = state.RecordProgress(6000, 6000, policy, media, DateTime.UtcNow);

        result.WasNewlyCompleted.Should().BeFalse();
        state.PlayCount.Should().Be(2);
    }

    [Test]
    public void RecordProgress_ShouldCompleteMusicByMinDuration()
    {
        var state = new UserMediaState();
        var media = new MusicTrack { Title = "Track" };
        var policy = new PlaybackProgressPolicy(IsMusic: true, CompletedThresholdPercent: 90, CompletedMinDurationSeconds: 30);

        var result = state.RecordProgress(30, 180, policy, media, DateTime.UtcNow);

        result.IsCompleted.Should().BeTrue();
        result.WasNewlyCompleted.Should().BeTrue();
    }
}
