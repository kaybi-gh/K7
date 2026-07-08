using FluentAssertions;
using K7.Server.Application.Common.Services;

namespace K7.Server.Application.UnitTests.Services;

public class EpisodeStillTimestampSelectorTests
{
    [Test]
    public void SelectTimestamp_ShouldPreferTargetInContentWindow_WhenNoKeyframes()
    {
        var timestamp = EpisodeStillTimestampSelector.SelectTimestamp(
            durationSeconds: 3600,
            introEndSeconds: 90,
            keyframeTimestamps: [],
            blackFrameTimestamps: []);

        timestamp.Should().BeApproximately(90 + 30 + (3600 * 0.90 - 120) * 0.25, 1);
    }

    [Test]
    public void SelectTimestamp_ShouldPickNearestKeyframe_WhenKeyframesExist()
    {
        var keyframes = new[] { 500.0, 520.0, 540.0 };

        var timestamp = EpisodeStillTimestampSelector.SelectTimestamp(
            durationSeconds: 3600,
            introEndSeconds: null,
            keyframeTimestamps: keyframes,
            blackFrameTimestamps: []);

        keyframes.Should().Contain(timestamp);
    }

    [Test]
    public void SelectTimestamp_ShouldAvoidBlackFrames_WhenPossible()
    {
        var keyframes = new[] { 500.0, 520.0, 540.0 };

        var timestamp = EpisodeStillTimestampSelector.SelectTimestamp(
            durationSeconds: 3600,
            introEndSeconds: null,
            keyframeTimestamps: keyframes,
            blackFrameTimestamps: [520.0]);

        timestamp.Should().NotBe(520.0);
    }

    [Test]
    public void GetContentWindow_ShouldExtendPastIntro_WhenIntroDetected()
    {
        var (windowStart, windowEnd) = EpisodeStillTimestampSelector.GetContentWindow(3600, 120);

        windowStart.Should().Be(150);
        windowEnd.Should().Be(3240);
    }
}
