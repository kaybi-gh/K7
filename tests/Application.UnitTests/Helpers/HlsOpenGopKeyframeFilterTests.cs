using K7.Server.Domain.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsOpenGopKeyframeFilterTests
{
    [Test]
    public void FilterSafeKeyframeTimestamps_ShouldKeepClosedGopCuts()
    {
        // Decode order: K then higher-PTS frames only.
        var packets = new (long, bool)[]
        {
            (0, true),
            (41, false),
            (83, false),
            (1000, true),
            (1041, false),
            (2000, true)
        };

        HlsOpenGopKeyframeFilter.FilterSafeKeyframeTimestamps(packets)
            .Should().Equal(0L, 1000L, 2000L);
    }

    [Test]
    public void FilterSafeKeyframeTimestamps_ShouldDropOpenGopUnsafeCut()
    {
        // Office-like CRA: K at 28028 then 6 B-frames with lower PTS (ffmpeg -f segment
        // drops those B-frames when cutting on this keyframe).
        var packets = new List<(long, bool)>
        {
            (0, true),
            (41, false),
            (17601, true),
            (17642, false),
            (27819, false),
            (28028, true),
            (27903, false),
            (27861, false),
            (27819, false),
            (27778, false),
            (27736, false),
            (27986, false),
            (35285, true),
            (35326, false)
        };

        HlsOpenGopKeyframeFilter.FilterSafeKeyframeTimestamps(packets)
            .Should().Equal(0L, 17601L, 35285L);
    }

    [Test]
    public void FilterSafeKeyframeTimestamps_ShouldClearUnsafeState_AtNextKeyframe()
    {
        var packets = new (long, bool)[]
        {
            (0, true),
            (1000, true),
            (900, false),
            (2000, true),
            (2041, false)
        };

        HlsOpenGopKeyframeFilter.FilterSafeKeyframeTimestamps(packets)
            .Should().Equal(0L, 2000L);
    }
}
