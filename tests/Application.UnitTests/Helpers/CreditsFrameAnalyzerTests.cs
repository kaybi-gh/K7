using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class CreditsFrameAnalyzerTests
{
    private const double Duration = 440;

    // Stats measured on real episodes: a cartoon scene, credit cards on a flat blue background,
    // scrolling credits on black, a dark story scene and an empty black frame.
    private static readonly CreditsFrameAnalyzer.FrameStats Story = new(0.38, 0.58, 202, 219, 114);
    private static readonly CreditsFrameAnalyzer.FrameStats BlueCard = new(0.86, 0.08, 140, 205, 247);
    private static readonly CreditsFrameAnalyzer.FrameStats BlackCard = new(0.98, 0.011, 0, 0, 0);
    private static readonly CreditsFrameAnalyzer.FrameStats DarkScene = new(0.86, 0.046, 4, 11, 14);
    private static readonly CreditsFrameAnalyzer.FrameStats Blank = new(1.0, 0, 0, 0, 0);

    [Test]
    public void ComputeStats_ShouldMeasureBackgroundAndInk_WhenFrameIsLetteringOnFlatColour()
    {
        var frame = new byte[CreditsFrameAnalyzer.FrameBytes];
        var pixels = CreditsFrameAnalyzer.FrameWidth * CreditsFrameAnalyzer.FrameHeight;
        for (var i = 0; i < pixels; i++)
        {
            var isInk = i % 10 == 0;
            frame[i * 3] = isInk ? (byte)255 : (byte)140;
            frame[i * 3 + 1] = isInk ? (byte)255 : (byte)205;
            frame[i * 3 + 2] = isInk ? (byte)255 : (byte)247;
        }

        var stats = CreditsFrameAnalyzer.ComputeStats(frame);

        stats.BackgroundShare.Should().BeApproximately(0.9, 0.001);
        stats.InkShare.Should().BeApproximately(0.1, 0.001);
        stats.BackgroundRed.Should().BeApproximately(140, 0.001);
        stats.BackgroundBlue.Should().BeApproximately(247, 0.001);
    }

    [Test]
    public void FindCreditsRun_ShouldFindCards_WhenTheyRunToTheEnd()
    {
        // 220 sampled seconds: story, then 17s of cards, then 4s of black logo up to the end.
        var frames = Repeat(Story, 199).Concat(Repeat(BlueCard, 13)).Concat(Repeat(BlackCard, 4)).Concat(Repeat(Blank, 4)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().NotBeNull();
        run!.Value.StartSeconds.Should().Be(419);
        run.Value.EndSeconds.Should().Be(Duration);
    }

    [Test]
    public void FindCreditsRun_ShouldReturnNull_WhenCardsAreShorterThanMinimum()
    {
        var frames = Repeat(Story, 210).Concat(Repeat(BlueCard, 10)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().BeNull();
    }

    [Test]
    public void FindCreditsRun_ShouldReturnNull_WhenCardsAreFarFromTheEnd()
    {
        // A title card sequence in the middle of the tail, followed by two minutes of story.
        var frames = Repeat(Story, 60).Concat(Repeat(BlackCard, 40)).Concat(Repeat(Story, 120)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().BeNull();
    }

    [Test]
    public void FindCreditsRun_ShouldNotStartInDarkScene_WhenCreditsFollowIt()
    {
        var frames = Repeat(Story, 100).Concat(Repeat(DarkScene, 60)).Concat(Repeat(BlackCard, 60)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().NotBeNull();
        run!.Value.StartSeconds.Should().Be(380);
    }

    [Test]
    public void FindCreditsRun_ShouldReturnNull_WhenTailIsOnlyBlankFrames()
    {
        var frames = Repeat(Story, 180).Concat(Repeat(Blank, 40)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().BeNull();
    }

    [Test]
    public void FindCreditsRun_ShouldBridgeShortInterruptions_WhenCardsResume()
    {
        // Three seconds of footage between two blocks of cards.
        var frames = Repeat(Story, 170)
            .Concat(Repeat(BlackCard, 20))
            .Concat(Repeat(Story, 3))
            .Concat(Repeat(BlackCard, 27))
            .ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().NotBeNull();
        run!.Value.StartSeconds.Should().Be(390);
        run.Value.EndSeconds.Should().Be(Duration);
    }

    [Test]
    public void FitToSpan_ShouldRepeatLastFrame_WhenSamplingStopsBeforeTheEnd()
    {
        var frames = Repeat(Story, 200).Concat(Repeat(BlackCard, 12)).ToList();

        var fitted = CreditsFrameAnalyzer.FitToSpan(frames, sampledSeconds: 220);

        fitted.Should().HaveCount(220);
        fitted[^1].Should().Be(BlackCard);
        fitted[211].Should().Be(BlackCard);
        fitted[199].Should().Be(Story);
    }

    [Test]
    public void MayStartEarlier_ShouldBeTrue_WhenFramesOpenOnCards()
    {
        var frames = Repeat(BlackCard, 90).ToList();

        CreditsFrameAnalyzer.MayStartEarlier(frames).Should().BeTrue();
    }

    [Test]
    public void MayStartEarlier_ShouldBeTrue_WhenACardIsWithinBridgingDistanceOfTheStart()
    {
        // Five seconds of footage can sit inside credits: the cards may resume before them.
        var frames = Repeat(Story, 5).Concat(Repeat(BlackCard, 85)).ToList();

        CreditsFrameAnalyzer.MayStartEarlier(frames).Should().BeTrue();
    }

    [Test]
    public void MayStartEarlier_ShouldBeTrue_WhenFramesOpenOnBlank()
    {
        var frames = Repeat(Blank, 3).Concat(Repeat(Story, 87)).ToList();

        CreditsFrameAnalyzer.MayStartEarlier(frames).Should().BeTrue();
    }

    [Test]
    public void MayStartEarlier_ShouldBeFalse_WhenFramesOpenOnFootage()
    {
        var frames = Repeat(Story, 6).Concat(Repeat(BlackCard, 84)).ToList();

        CreditsFrameAnalyzer.MayStartEarlier(frames).Should().BeFalse();
    }

    [Test]
    public void FindCreditsRun_ShouldIgnoreEmptySlots_WhenOnlyTheEndWasDecoded()
    {
        // The sampler leaves slots empty before the part it decoded.
        var frames = Repeat(default, 130).Concat(Repeat(Story, 60)).Concat(Repeat(BlackCard, 30)).ToList();

        var run = CreditsFrameAnalyzer.FindCreditsRun(frames, firstFrameSeconds: 220, Duration);

        run.Should().NotBeNull();
        run!.Value.StartSeconds.Should().Be(410);
        run.Value.EndSeconds.Should().Be(Duration);
    }

    [Test]
    public void FitToSpan_ShouldDropExtraFrames_WhenSamplingOverruns()
    {
        var frames = Repeat(Story, 220).Concat(Repeat(BlackCard, 2)).ToList();

        var fitted = CreditsFrameAnalyzer.FitToSpan(frames, sampledSeconds: 220);

        fitted.Should().HaveCount(220);
        fitted[^1].Should().Be(Story);
    }

    [Test]
    public void FitToSpan_ShouldFillWithEmptyFrames_WhenNothingWasDecoded()
    {
        var fitted = CreditsFrameAnalyzer.FitToSpan([], sampledSeconds: 30);

        fitted.Should().HaveCount(30);
        CreditsFrameAnalyzer.FindCreditsRun(fitted, firstFrameSeconds: 410, Duration).Should().BeNull();
    }

    [Test]
    public void FitToSpan_ShouldLeaveFramesAlone_WhenSpanIsFullySampled()
    {
        var frames = Repeat(Story, 220).ToList();

        var fitted = CreditsFrameAnalyzer.FitToSpan(frames, sampledSeconds: 220);

        fitted.Should().BeSameAs(frames);
    }

    private static IEnumerable<CreditsFrameAnalyzer.FrameStats> Repeat(CreditsFrameAnalyzer.FrameStats frame, int count) =>
        Enumerable.Repeat(frame, count);
}
