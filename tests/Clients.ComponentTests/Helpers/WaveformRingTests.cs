using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class WaveformRingTests
{
    // A 200 x 200 ring whose top left corner is at (100, 50): its center is (200, 150).
    [TestCase(200, 60, 0.0)]
    [TestCase(290, 150, 0.25)]
    [TestCase(200, 240, 0.5)]
    [TestCase(110, 150, 0.75)]
    public void FractionAt_ShouldStartAtTheTopAndRunClockwise(double x, double y, double expected)
    {
        WaveformRing.FractionAt(x, y, 100, 50, 200, 200).Should().BeApproximately(expected, 0.001);
    }

    [Test]
    public void BarLevel_ShouldTakeTheLoudestPeakOfItsSlice()
    {
        var peaks = new float[WaveformRing.BarCount * 2];
        peaks[0] = 0.2f;
        peaks[1] = 0.9f;
        peaks[2] = 0.4f;

        WaveformRing.BarLevel(peaks, 0).Should().BeApproximately(0.9, 0.0001);
        WaveformRing.BarLevel(peaks, 1).Should().BeApproximately(0.4, 0.0001);
    }

    [Test]
    public void BarLevel_ShouldStillReadEveryBar_WhenThereAreFewerPeaksThanBars()
    {
        float[] peaks = [0.5f, 1f];

        WaveformRing.BarLevel(peaks, 0).Should().BeApproximately(0.5, 0.0001);
        WaveformRing.BarLevel(peaks, WaveformRing.BarCount - 1).Should().BeApproximately(1, 0.0001);
    }

    [Test]
    public void BarLevel_ShouldBeFlat_WhenTheTrackHasNoPeaks()
    {
        WaveformRing.BarLevel(null, 12).Should().Be(0);
        WaveformRing.BarLevel([], 12).Should().Be(0);
    }

    [Test]
    public void PlainMaskStyle_ShouldDrawOneCircle_AndNoBar()
    {
        var style = WaveformRing.PlainMaskStyle();
        var svg = Uri.UnescapeDataString(style);

        style.Should().StartWith("-webkit-mask-image: url(").And.Contain(" mask-image: url(");
        svg.Should().Contain("<circle").And.NotContain("<path");
    }

    [Test]
    public void MaskStyle_ShouldDrawOneBarPerSlot_WithOrWithoutPeaks()
    {
        foreach (var peaks in (float[]?[])[null, [0.1f, 0.8f, 0.3f]])
        {
            var style = WaveformRing.MaskStyle(peaks);
            var svg = Uri.UnescapeDataString(style);

            style.Should().StartWith("-webkit-mask-image: url(").And.Contain(" mask-image: url(");
            svg.Split("M").Length.Should().BeGreaterThanOrEqualTo(WaveformRing.BarCount);
        }
    }
}
