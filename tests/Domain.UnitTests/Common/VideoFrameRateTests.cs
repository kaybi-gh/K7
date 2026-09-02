using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class VideoFrameRateTests
{
    [Test]
    public void FromProbe_ShouldPreferAvgFrameRate()
    {
        VideoFrameRate.FromProbe(23.976, 24000)
            .Should().BeApproximately(23.976f, 0.001f);
    }

    [Test]
    public void FromProbe_ShouldFallBackToFrameRate_WhenAvgIsJunk()
    {
        VideoFrameRate.FromProbe(0, 24)
            .Should().Be(24f);
    }

    [Test]
    public void FromProbe_ShouldBeNull_WhenBothAreTimebaseJunk()
    {
        VideoFrameRate.FromProbe(0, 90000).Should().BeNull();
    }

    [Test]
    public void IsMissing_ShouldBeTrue_WhenNullOrNotPlausible()
    {
        VideoFrameRate.IsMissing(null).Should().BeTrue();
        VideoFrameRate.IsMissing(0f).Should().BeTrue();
        VideoFrameRate.IsMissing(1f).Should().BeTrue();
        VideoFrameRate.IsMissing(125f).Should().BeTrue();
        VideoFrameRate.IsMissing(90000f).Should().BeTrue();
    }

    [Test]
    public void IsMissing_ShouldBeFalse_WhenPlausible()
    {
        VideoFrameRate.IsMissing(23.976f).Should().BeFalse();
        VideoFrameRate.IsMissing(24f).Should().BeFalse();
        VideoFrameRate.IsMissing(59.94f).Should().BeFalse();
    }

    [Test]
    public void ParseRate_ShouldParseFractionAndPlain()
    {
        VideoFrameRate.ParseRate("24000/1001").Should().BeApproximately(23.976, 0.001);
        VideoFrameRate.ParseRate("24").Should().Be(24);
        VideoFrameRate.ParseRate("24.0").Should().Be(24);
        VideoFrameRate.ParseRate("0/0").Should().Be(0);
        VideoFrameRate.ParseRate("N/A").Should().Be(0);
        VideoFrameRate.ParseRate(null).Should().Be(0);
        VideoFrameRate.ParseRate("").Should().Be(0);
    }

    [Test]
    public void FromRateStrings_ShouldPreferAvgThenReal()
    {
        VideoFrameRate.FromRateStrings("24000/1001", "24/1")
            .Should().BeApproximately(23.976f, 0.001f);
        VideoFrameRate.FromRateStrings("0/0", "24/1").Should().Be(24f);
        VideoFrameRate.FromRateStrings("0/0", "90000/1").Should().BeNull();
    }
}
