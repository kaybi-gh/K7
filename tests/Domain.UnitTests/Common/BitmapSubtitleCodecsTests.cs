using K7.Server.Domain.Common;

namespace K7.Server.Domain.UnitTests.Common;

[TestFixture]
public class BitmapSubtitleCodecsTests
{
    [TestCase("hdmv_pgs_subtitle", true)]
    [TestCase("HDMV_PGS_SUBTITLE", true)]
    [TestCase("pgssub", true)]
    [TestCase(" pgs ", true)]
    [TestCase("dvd_subtitle", false)]
    [TestCase("dvb_subtitle", false)]
    [TestCase("subrip", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsPgs_ShouldMatchOnlyPgsCodecNames(string? codec, bool expected)
    {
        BitmapSubtitleCodecs.IsPgs(codec).Should().Be(expected);
    }

    [Test]
    public void CanRenderOnClient_ShouldBeTrue_WhenDeviceDeclaresPgsAndTrackIsPgs()
    {
        BitmapSubtitleCodecs.CanRenderOnClient(["webvtt", BitmapSubtitleCodecs.Pgs], "hdmv_pgs_subtitle")
            .Should().BeTrue();
    }

    [Test]
    public void CanRenderOnClient_ShouldBeFalse_WhenDeviceOnlyDeclaresText()
    {
        BitmapSubtitleCodecs.CanRenderOnClient(["webvtt"], "hdmv_pgs_subtitle").Should().BeFalse();
        BitmapSubtitleCodecs.CanRenderOnClient(null, "hdmv_pgs_subtitle").Should().BeFalse();
    }

    [Test]
    public void CanRenderOnClient_ShouldBeFalse_WhenTrackIsAnotherBitmapCodec()
    {
        BitmapSubtitleCodecs.CanRenderOnClient(["webvtt", BitmapSubtitleCodecs.Pgs], "dvd_subtitle")
            .Should().BeFalse();
    }
}
