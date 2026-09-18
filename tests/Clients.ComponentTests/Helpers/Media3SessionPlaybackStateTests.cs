using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class Media3SessionPlaybackStateTests
{
    [Test]
    public void ForSession_ShouldKeepEnded_WhenPlaylistIsEmptyEvenIfQueueHasNext()
    {
        Media3SessionPlaybackState.ForSession(
                Media3SessionPlaybackState.Ended,
                mediaItemCount: 0,
                hasNext: true)
            .Should().Be(Media3SessionPlaybackState.Ended);
    }

    [Test]
    public void ForSession_ShouldForceIdle_WhenReadyWithEmptyPlaylist()
    {
        Media3SessionPlaybackState.ForSession(
                Media3SessionPlaybackState.Ready,
                mediaItemCount: 0,
                hasNext: true)
            .Should().Be(Media3SessionPlaybackState.Idle);
    }

    [Test]
    public void ForSession_ShouldAdvertiseReady_WhenEndedWithItemsAndNext()
    {
        Media3SessionPlaybackState.ForSession(
                Media3SessionPlaybackState.Ended,
                mediaItemCount: 1,
                hasNext: true)
            .Should().Be(Media3SessionPlaybackState.Ready);
    }

    [Test]
    public void ForSession_ShouldKeepEnded_WhenNoNext()
    {
        Media3SessionPlaybackState.ForSession(
                Media3SessionPlaybackState.Ended,
                mediaItemCount: 1,
                hasNext: false)
            .Should().Be(Media3SessionPlaybackState.Ended);
    }
}
