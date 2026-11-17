using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class PgsTimelineTests
{
    [Test]
    public void FrameAt_ShouldReturnTheLastScreenStarted_WhenTimeIsBetweenTwoScreens()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(10));
        timeline.Add(Screen(20));
        timeline.MarkLoaded(60);

        timeline.FrameAt(9.9).Should().BeNull();
        timeline.FrameAt(10)!.TimeSeconds.Should().Be(10);
        timeline.FrameAt(19.9)!.TimeSeconds.Should().Be(10);
        timeline.FrameAt(25)!.TimeSeconds.Should().Be(20);
    }

    [Test]
    public void FrameAt_ShouldReturnNull_WhenTheLastScreenIsAClear()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(10));
        timeline.Add(Clear(12));
        timeline.MarkLoaded(60);

        timeline.FrameAt(11).Should().NotBeNull();
        timeline.FrameAt(12).Should().BeNull();
        timeline.FrameAt(30).Should().BeNull();
    }

    [Test]
    public void FrameAt_ShouldReturnNull_WhenTimeIsPastWhatHasLoaded()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(10));

        // The clear that ends this screen may not have arrived yet.
        timeline.FrameAt(10).Should().NotBeNull();
        timeline.FrameAt(11).Should().BeNull();

        timeline.MarkLoaded(15);
        timeline.FrameAt(11).Should().NotBeNull();
    }

    [Test]
    public void Add_ShouldKeepScreensOrdered_WhenOneArrivesLate()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(20));
        timeline.Add(Screen(10));
        timeline.MarkLoaded(60);

        timeline.FrameAt(15)!.TimeSeconds.Should().Be(10);
    }

    [Test]
    public void Prune_ShouldDropOldScreens_ButKeepTheOneStillOnScreen()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(10));
        timeline.Add(Clear(12));
        timeline.Add(Screen(20));
        timeline.MarkLoaded(1000);

        timeline.Prune(20 + PgsTimeline.KeepBehindSeconds + 100);

        timeline.Count.Should().Be(1);
        timeline.FrameAt(500)!.TimeSeconds.Should().Be(20);
    }

    [Test]
    public void Reset_ShouldForgetScreensAndLoadedRange()
    {
        var timeline = new PgsTimeline();
        timeline.Add(Screen(10));
        timeline.MarkLoaded(60);

        timeline.Reset();

        timeline.Count.Should().Be(0);
        timeline.LoadedUntilSeconds.Should().Be(0);
        timeline.FrameAt(10).Should().BeNull();
    }

    private static PgsFrame Screen(double seconds) =>
        new(seconds, 1920, 1080, [new PgsFrameObject(0, 0, 1, 1, [255, 255, 255, 255])]);

    private static PgsFrame Clear(double seconds) => new(seconds, 1920, 1080, []);
}
