using K7.Clients.Shared.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class AndroidHdmiFrameRateMatchingTests
{
    [Test]
    public void ClassifyCadence_ShouldBeUnknown_WhenContentFpsIsMissing()
    {
        AndroidHdmiFrameRateMatching.ClassifyCadence(0f, 59.94f)
            .Should().Be(HdmiCadenceKind.Unknown);
        AndroidHdmiFrameRateMatching.DescribeCadence(HdmiCadenceKind.Unknown)
            .Should().Be("unknown");
    }

    [Test]
    public void ClassifyCadence_ShouldBe25x_WhenFilmOn5994()
    {
        AndroidHdmiFrameRateMatching.ClassifyCadence(23.976f, 59.94f)
            .Should().Be(HdmiCadenceKind.Match25x);
    }

    [Test]
    public void RateScore_ShouldPrefer24Hz_WhenContentIs23976()
    {
        var at24 = AndroidHdmiFrameRateMatching.RateScore(24f, 23.976f);
        var at60 = AndroidHdmiFrameRateMatching.RateScore(60f, 23.976f);
        at24.Should().BeLessThan(at60);
        AndroidHdmiFrameRateMatching.ShouldSwitch(at60, at24).Should().BeTrue();
    }

    [Test]
    public void RateScore_ShouldPrefer5994_WhenOnly60And5994Exist()
    {
        var at5994 = AndroidHdmiFrameRateMatching.RateScore(59.94f, 23.976f);
        var at60 = AndroidHdmiFrameRateMatching.RateScore(60f, 23.976f);
        at5994.Should().BeLessThan(at60);
    }

    [Test]
    public void ModeScore_ShouldPreferCurrentResolution_WhenRateIsEqual()
    {
        var native24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 3840, 2160, 23.976f, 3840, 2160);
        var media24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 1920, 1080, 23.976f, 3840, 2160);
        native24.Should().BeLessThan(media24);
    }

    [Test]
    public void ModeScore_ShouldPrefer1080p24_Over4k5994_WhenNo4k24()
    {
        var at1080p24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 1920, 1080, 23.976f, 3840, 2160, 1920, 800);
        var at4k5994 = AndroidHdmiFrameRateMatching.ModeScore(
            59.94f, 3840, 2160, 23.976f, 3840, 2160, 1920, 800);
        at1080p24.Should().BeLessThan(at4k5994);
        AndroidHdmiFrameRateMatching.ShouldSwitch(at4k5994, at1080p24).Should().BeTrue();
    }

    [Test]
    public void ModeScore_ShouldPrefer4k24_Over1080p24()
    {
        var at4k24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 3840, 2160, 23.976f, 3840, 2160, 1920, 800);
        var at1080p24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 1920, 1080, 23.976f, 3840, 2160, 1920, 800);
        at4k24.Should().BeLessThan(at1080p24);
    }

    [Test]
    public void ModeScore_ShouldPrefer1080p24_Over4k24_WhenPreferContentResolution()
    {
        var at4k24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 3840, 2160, 23.976f, 3840, 2160, 1920, 802, preferContentResolution: true);
        var at1080p24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 1920, 1080, 23.976f, 3840, 2160, 1920, 802, preferContentResolution: true);
        at1080p24.Should().BeLessThan(at4k24);
        AndroidHdmiFrameRateMatching.ShouldSwitch(at4k24, at1080p24).Should().BeTrue();
    }

    [Test]
    public void ModeScore_ShouldStillPrefer4k24_Over1080p5994_WhenPreferContentResolution()
    {
        var at4k24 = AndroidHdmiFrameRateMatching.ModeScore(
            24f, 3840, 2160, 23.976f, 3840, 2160, 1920, 802, preferContentResolution: true);
        var at1080p5994 = AndroidHdmiFrameRateMatching.ModeScore(
            59.94f, 1920, 1080, 23.976f, 3840, 2160, 1920, 802, preferContentResolution: true);
        at4k24.Should().BeLessThan(at1080p5994);
    }

    [Test]
    public void ModeScore_ShouldRejectDowngradeBelowContent()
    {
        AndroidHdmiFrameRateMatching.ModeScore(
                24f, 1280, 720, 23.976f, 3840, 2160, 1920, 800)
            .Should().Be(double.MaxValue);
    }

    [Test]
    public void QualifiesRefreshRate_ShouldAccept24For23976()
    {
        AndroidHdmiFrameRateMatching.QualifiesRefreshRate(24f, 23.976f).Should().BeTrue();
        AndroidHdmiFrameRateMatching.QualifiesRefreshRate(59.94f, 23.976f).Should().BeTrue();
        AndroidHdmiFrameRateMatching.QualifiesRefreshRate(60f, 23.976f).Should().BeFalse();
    }

    [Test]
    public void ShouldSwitch_ShouldBeTrue_When5994LosesTo24()
    {
        var at5994 = AndroidHdmiFrameRateMatching.RateScore(59.94f, 23.976f);
        var at24 = AndroidHdmiFrameRateMatching.RateScore(24f, 23.976f);
        AndroidHdmiFrameRateMatching.ShouldSwitch(at5994, at24).Should().BeTrue();
    }

    [Test]
    public void ClassifyCadence_ShouldBePulldown32_When23976On60Hz()
    {
        AndroidHdmiFrameRateMatching.ClassifyCadence(23.976f, 60f)
            .Should().Be(HdmiCadenceKind.Pulldown32);
        AndroidHdmiFrameRateMatching.IsCadenceWarning(HdmiCadenceKind.Pulldown32)
            .Should().BeTrue();
    }

    [Test]
    public void ClassifyCadence_ShouldBe1x_When23976On24Hz()
    {
        AndroidHdmiFrameRateMatching.ClassifyCadence(23.976f, 24f)
            .Should().Be(HdmiCadenceKind.Match1x);
    }

    [Test]
    public void ClassifyCadence_ShouldBe25x_When23976On5994Hz()
    {
        AndroidHdmiFrameRateMatching.ClassifyCadence(23.976f, 59.94f)
            .Should().Be(HdmiCadenceKind.Match25x);
    }

    [Test]
    public void IsHdMode_ShouldBeFalse_WhenBelow720p()
    {
        AndroidHdmiFrameRateMatching.IsHdMode(720, 480).Should().BeFalse();
        AndroidHdmiFrameRateMatching.IsHdMode(1920, 1080).Should().BeTrue();
    }
}
